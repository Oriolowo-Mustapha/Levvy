using Hangfire;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire background job that persists finalized ASR caption segments off the hot WebSocket loop.
/// </summary>
public sealed class CaptionPersistenceJob(
    ISessionRepository repo,
    ILogger<CaptionPersistenceJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(
        Guid sessionId,
        long offsetMs,
        string text,
        string sourceLanguage,
        CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogDebug("Session {SessionId} not found in DB while persisting caption at {OffsetMs}ms.", sessionId, offsetMs);
            return;
        }

        session.Segments.Add(new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OffsetMs = offsetMs,
            Text = text,
            SourceLanguage = sourceLanguage,
            CreatedAtUtc = DateTime.UtcNow
        });

        await repo.SaveChangesAsync(ct);
    }
}
