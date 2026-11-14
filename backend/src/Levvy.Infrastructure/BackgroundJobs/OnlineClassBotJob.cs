using Hangfire;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire worker for the online-class bot lifecycle (plan.md Phase 2, Mode A):
/// dispatches the "Levvy Note-Taker" bot through IMeetingBotConnector, then polls its
/// status every 30s via MonitorAsync until the bot is in-call, the call ends, or it fails.
/// Webhook events (call_ended / captions) drive the rest of the lifecycle.
/// </summary>
public sealed class OnlineClassBotJob(
    ISessionRepository repo,
    IMeetingBotConnector connector,
    IBackgroundJobScheduler scheduler,
    ILogger<OnlineClassBotJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task DispatchAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(sessionId, ct);
        if (session?.ExternalMeetingUrl is null)
        {
            logger.LogDebug("Session {SessionId} has no external meeting URL; skipping bot dispatch.", sessionId);
            return;
        }

        var dispatch = await connector.DispatchBotAsync(
            session.Id,
            session.ExternalMeetingUrl,
            "Levvy Note-Taker",
            ct);

        session.BotStatus = dispatch.Status;
        session.ExternalBotId = dispatch.ExternalBotId;
        await repo.SaveChangesAsync(ct);

        if (dispatch.Status == "unconfigured")
        {
            logger.LogWarning(
                "No meeting-bot API key configured (MEETING_BOT_API_KEY). Session {SessionId} falls back to Mode B (browser tab audio).",
                sessionId);
            return;
        }

        scheduler.EnqueueMonitorOnlineClassBot(sessionId);
    }

    /// <summary>Test/back-compat overload: monitor with attempt counter starting at zero.</summary>
    public Task MonitorAsync(Guid sessionId, CancellationToken ct) => MonitorAsync(sessionId, 0, ct);

    [AutomaticRetry(Attempts = 3)]
    public async Task MonitorAsync(Guid sessionId, int attempt, CancellationToken ct)
    {
        const int maxAttempts = 40; // 40 polls x 30s spacing ≈ 20 minutes of bot-join patience.

        var session = await repo.GetWithTranscriptAsync(sessionId, ct);
        if (session?.ExternalBotId is null || session.EndedAtUtc is not null)
            return;

        // Terminal or in-call states are webhook-driven; only keep polling while pending/joining.
        if (session.BotStatus is "active" or "ended" or "failed")
            return;

        // Audit fix (M6): cap the poll chain so a stuck bot can't poll forever / self-duplicate.
        if (attempt >= maxAttempts)
        {
            logger.LogWarning(
                "Online class bot for session {SessionId} still not in-call after {Attempts} polls; giving up monitoring.",
                sessionId, attempt);
            return;
        }

        var status = await connector.GetBotStatusAsync(session.Id, session.ExternalBotId, ct);

        session.BotStatus = status.Status;
        await repo.SaveChangesAsync(ct);

        if (status.Status is "pending" or "joining")
        {
            scheduler.EnqueueMonitorOnlineClassBot(sessionId, attempt + 1);
            return;
        }

        logger.LogInformation(
            "Online class bot for session {SessionId} reached terminal status {Status}.",
            sessionId,
            status.Status);
    }
}
