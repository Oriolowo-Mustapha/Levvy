using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Decorator/Strategy wrapper supporting Groq (default) or Deepgram (via ASR_PROVIDER=deepgram)
/// as primary ASR with automatic failover to the configured backup provider.
/// Audit fix (M4): a circuit breaker skips the primary provider for a cool-down window after
/// repeated consecutive failures, instead of re-hitting a dead endpoint on every chunk.
/// </summary>
public class ResilientAsrStreamer(
    GroqAsrStreamer primary,
    DeepgramAsrStreamer fallback,
    IConfiguration cfg,
    ILogger<ResilientAsrStreamer> logger) : IAsrStreamer
{
    private const int FailureThreshold = 3;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);

    private int _consecutivePrimaryFailures;
    private DateTimeOffset _primaryCircuitOpenUntil = DateTimeOffset.MinValue;
    private readonly object _circuitLock = new();

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

        var skipPrimary = IsPrimaryCircuitOpen();
        if (skipPrimary && hasDeepgramKey)
        {
            logger.LogWarning(
                "Groq ASR circuit open (after {Failures} consecutive failures); skipping straight to Deepgram for chunk at {OffsetMs}ms.",
                FailureThreshold, chunkStartOffsetMs);
            using var fallbackStream = new MemoryStream(payloadBytes, writable: false);
            return await fallback.TranscribeChunkAsync(fallbackStream, chunkStartOffsetMs, sourceLanguage, ct);
        }

        try
        {
            using var primaryStream = new MemoryStream(payloadBytes, writable: false);
            var result = await primary.TranscribeChunkAsync(primaryStream, chunkStartOffsetMs, sourceLanguage, ct);
            ResetCircuit();
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && hasDeepgramKey)
        {
            logger.LogWarning(ex, "Groq ASR failed for chunk at {OffsetMs}ms; falling back to Deepgram.", chunkStartOffsetMs);
            RecordPrimaryFailure();
            using var fallbackStream = new MemoryStream(payloadBytes, writable: false);
            return await fallback.TranscribeChunkAsync(fallbackStream, chunkStartOffsetMs, sourceLanguage, ct);
        }
    }

    private bool IsPrimaryCircuitOpen()
    {
        lock (_circuitLock)
        {
            return _consecutivePrimaryFailures >= FailureThreshold &&
                   DateTimeOffset.UtcNow < _primaryCircuitOpenUntil;
        }
    }

    private void RecordPrimaryFailure()
    {
        lock (_circuitLock)
        {
            _consecutivePrimaryFailures++;
            if (_consecutivePrimaryFailures >= FailureThreshold)
            {
                _primaryCircuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitOpenDuration);
                logger.LogWarning(
                    "Groq ASR circuit OPENED for {Seconds}s after {Failures} consecutive failures.",
                    CircuitOpenDuration.TotalSeconds, _consecutivePrimaryFailures);
            }
        }
    }

    private void ResetCircuit()
    {
        lock (_circuitLock)
        {
            _consecutivePrimaryFailures = 0;
            _primaryCircuitOpenUntil = DateTimeOffset.MinValue;
        }
    }
}
