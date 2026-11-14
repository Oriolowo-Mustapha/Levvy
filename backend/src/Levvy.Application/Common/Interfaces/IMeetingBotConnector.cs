namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Abstraction over external meeting-bot providers (Recall.ai / MeetingBaas style) so
/// Application handlers stay decoupled from the concrete HTTP integration in Infrastructure.
/// Bot status values match Session.BotStatus: pending | joining | active | ended | failed | unconfigured.
/// </summary>
public interface IMeetingBotConnector
{
    /// <summary>
    /// Spawns a "Levvy Note-Taker" bot into the meeting. When no provider API key is configured,
    /// returns ("unconfigured", null) so callers can fall back to Mode B (browser tab audio).
    /// </summary>
    Task<MeetingBotDispatchResult> DispatchBotAsync(Guid sessionId, string meetingUrl, string botName, CancellationToken ct);

    /// <summary>Polls the provider for the bot's current lifecycle status.</summary>
    Task<MeetingBotStatusResult> GetBotStatusAsync(Guid sessionId, string externalBotId, CancellationToken ct);

    /// <summary>Best-effort removal of the bot from the meeting (never throws).</summary>
    Task RemoveBotAsync(string externalBotId, CancellationToken ct);
}

public sealed record MeetingBotDispatchResult(string? ExternalBotId, string Status);

public sealed record MeetingBotStatusResult(string Status, bool IsActive);
