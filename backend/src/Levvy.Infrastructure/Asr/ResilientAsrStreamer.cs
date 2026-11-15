using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Decorator/Strategy wrapper supporting Deepgram (default) or Groq (via ASR_PROVIDER=groq)
/// as primary ASR with automatic failover to the configured backup provider.
/// Includes a circuit breaker that skips the primary provider for a cool-down window after
/// repeated consecutive failures instead of re-hitting a dead endpoint on every chunk.
/// </summary>
public class ResilientAsrStreamer(
    GroqAsrStreamer groq,
    DeepgramAsrStreamer deepgram,
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
        var preferredProvider = (cfg["Asr:Provider"] ?? cfg["ASR_PROVIDER"] ?? "deepgram").Trim().ToLowerInvariant();

        // Deepgram is primary by default; fall back to Groq Whisper if Deepgram fails or isn't configured.
        // Explicit ASR_PROVIDER=groq flips the order (Groq primary, Deepgram fallback).
        var deepgramIsPrimary = (preferredProvider != "groq" && hasDeepgramKey) || (!hasGroqKey && hasDeepgramKey);

        IAsrStreamer primary = deepgramIsPrimary ? deepgram : groq;
        IAsrStreamer fallback = deepgramIsPrimary ? groq : deepgram;
        var primaryName = deepgramIsPrimary ? "Deepgram" : "Groq";
        var fallbackName = deepgramIsPrimary ? "Groq" : "Deepgram";
        var hasFallbackKey = deepgramIsPrimary ? hasGroqKey : hasDeepgramKey;

        if (IsPrimaryCircuitOpen() && hasFallbackKey)
        {
            logger.LogWarning(
                "{Primary} ASR circuit open (after {Failures} consecutive failures); skipping straight to {Fallback} for chunk at {OffsetMs}ms.",
                primaryName, FailureThreshold, fallbackName, chunkStartOffsetMs);
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
        catch (Exception ex) when (!ct.IsCancellationRequested && hasFallbackKey)
        {
            logger.LogWarning(
                ex,
                "{Primary} ASR failed for chunk at {OffsetMs}ms; falling back to {Fallback}.",
                primaryName, chunkStartOffsetMs, fallbackName);
            RecordPrimaryFailure(primaryName);
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

    private void RecordPrimaryFailure(string primaryName)
    {
        lock (_circuitLock)
        {
            _consecutivePrimaryFailures++;
            if (_consecutivePrimaryFailures >= FailureThreshold)
            {
                _primaryCircuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitOpenDuration);
                logger.LogWarning(
                    "{Primary} ASR circuit OPENED for {Seconds}s after {Failures} consecutive failures.",
                    primaryName, CircuitOpenDuration.TotalSeconds, _consecutivePrimaryFailures);
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
