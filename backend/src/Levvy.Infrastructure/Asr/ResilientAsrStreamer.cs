using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Decorator/Strategy wrapper supporting Groq (default) or Deepgram (via ASR_PROVIDER=deepgram)
/// as primary ASR with automatic failover to the configured backup provider.
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

        var hasGroqKey = !string.IsNullOrWhiteSpace(cfg["Groq:ApiKey"] ?? cfg["GROQ_API_KEY"]);
        var hasDeepgramKey = !string.IsNullOrWhiteSpace(cfg["Deepgram:ApiKey"] ?? cfg["DEEPGRAM_API_KEY"]);
        var preferredProvider = (cfg["Asr:Provider"] ?? cfg["ASR_PROVIDER"] ?? "groq").Trim().ToLowerInvariant();
        var useDeepgramFirst = preferredProvider == "deepgram" || (!hasGroqKey && hasDeepgramKey);

        if (useDeepgramFirst)
        {
            try
            {
                using var dgStream = new MemoryStream(payloadBytes, writable: false);
                return await fallback.TranscribeChunkAsync(dgStream, chunkStartOffsetMs, sourceLanguage, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && hasGroqKey)
            {
                logger.LogWarning(ex, "Deepgram ASR failed for chunk at {OffsetMs}ms; falling back to Groq.", chunkStartOffsetMs);
                using var groqStream = new MemoryStream(payloadBytes, writable: false);
                return await primary.TranscribeChunkAsync(groqStream, chunkStartOffsetMs, sourceLanguage, ct);
            }
        }

        try
        {
            using var primaryStream = new MemoryStream(payloadBytes, writable: false);
            return await primary.TranscribeChunkAsync(primaryStream, chunkStartOffsetMs, sourceLanguage, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && hasDeepgramKey)
        {
            logger.LogWarning(ex, "Groq ASR failed for chunk at {OffsetMs}ms; falling back to Deepgram.", chunkStartOffsetMs);
            using var fallbackStream = new MemoryStream(payloadBytes, writable: false);
            return await fallback.TranscribeChunkAsync(fallbackStream, chunkStartOffsetMs, sourceLanguage, ct);
        }
    }
}
