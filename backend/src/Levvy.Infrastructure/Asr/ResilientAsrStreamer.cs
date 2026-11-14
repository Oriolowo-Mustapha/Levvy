using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Decorator/Strategy wrapper that uses GroqAsrStreamer as primary and automatically falls back
/// to DeepgramAsrStreamer if Groq fails or rate-limits and Deepgram:ApiKey is configured.
/// </summary>
public class ResilientAsrStreamer(
    GroqAsrStreamer primary,
    DeepgramAsrStreamer fallback,
    IConfiguration cfg,
    ILogger<ResilientAsrStreamer> logger) : IAsrStreamer
{
    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeChunkAsync(
        Stream audioChunk,
        long chunkStartOffsetMs,
        string sourceLanguage,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await audioChunk.CopyToAsync(buffer, ct);
        var payloadBytes = buffer.ToArray();

        try
        {
            using var primaryStream = new MemoryStream(payloadBytes, writable: false);
            return await primary.TranscribeChunkAsync(primaryStream, chunkStartOffsetMs, sourceLanguage, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && !string.IsNullOrWhiteSpace(cfg["Deepgram:ApiKey"]))
        {
            logger.LogWarning(ex, "Groq ASR failed for chunk at {OffsetMs}ms; falling back to Deepgram.", chunkStartOffsetMs);
            using var fallbackStream = new MemoryStream(payloadBytes, writable: false);
            return await fallback.TranscribeChunkAsync(fallbackStream, chunkStartOffsetMs, sourceLanguage, ct);
        }
    }
}
