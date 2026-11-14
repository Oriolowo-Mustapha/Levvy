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
        // Audit fix (H6): no full session-graph load, and dedup-safe on Hangfire retries.
        var added = await repo.AddSegmentIfMissingAsync(new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OffsetMs = offsetMs,
            Text = text,
            SourceLanguage = sourceLanguage,
            CreatedAtUtc = DateTime.UtcNow
        }, ct);

        if (!added)
        {
            logger.LogDebug(
                "Duplicate caption segment for session {SessionId} at {OffsetMs}ms skipped (idempotent retry).",
                sessionId, offsetMs);
        }
    }
}
