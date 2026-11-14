using System.Net.Http.Headers;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Asr.Models;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Groq Whisper (whisper-large-v3-turbo) chunked transcription.
/// Maps segment timestamps (seconds -> ms) and adds chunkStartOffsetMs to build the receipt timeline.
/// </summary>
public class GroqAsrStreamer : IAsrStreamer
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GroqAsrStreamer(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
        if (_http.BaseAddress is null)
        {
            var baseUrl = _cfg["Groq:BaseUrl"] ?? _cfg["GROQ_BASE_URL"] ?? "https://api.groq.com/openai/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            _http.BaseAddress = new Uri(baseUrl);
        }
    }

    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeChunkAsync(
        Stream audioChunk,
        long chunkStartOffsetMs,
        string sourceLanguage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(audioChunk);
        if (chunkStartOffsetMs < 0)
            throw new ArgumentOutOfRangeException(nameof(chunkStartOffsetMs), "Chunk start offset cannot be negative.");

        var apiKey = _cfg["Groq:ApiKey"] ?? _cfg["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("GROQ_API_KEY is not configured in .env or environment variables.");

        var model = _cfg["Groq:AsrModel"] ?? _cfg["GROQ_ASR_MODEL"] ?? "whisper-large-v3-turbo";

        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(audioChunk);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(streamContent, "file", "chunk.webm");
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent("0"), "temperature");

        if (!string.IsNullOrWhiteSpace(sourceLanguage) &&
            !sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
            !sourceLanguage.Equals("mixed", StringComparison.OrdinalIgnoreCase))
        {
            form.Add(new StringContent(sourceLanguage.Trim().ToLowerInvariant()), "language");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions")
        {
            Content = form
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Groq ASR failed with status {(int)resp.StatusCode} ({resp.ReasonPhrase}): {body}",
                null,
                resp.StatusCode);
        }

        return ParseWhisperResponse(body, chunkStartOffsetMs, sourceLanguage);
    }

    public static IReadOnlyList<TranscriptSegment> ParseWhisperResponse(
        string json,
        long chunkStartOffsetMs,
        string requestedLanguage = "en")
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TranscriptSegment>();

        var payload = JsonSerializer.Deserialize<WhisperVerboseResponse>(json, JsonOpts);
        if (payload is null)
            return Array.Empty<TranscriptSegment>();

        var detectedLang = !string.IsNullOrWhiteSpace(payload.Language)
            ? NormalizeLanguageCode(payload.Language)
            : (string.IsNullOrWhiteSpace(requestedLanguage) ? "en" : requestedLanguage);

        var now = DateTime.UtcNow;
        var results = new List<TranscriptSegment>();

        if (payload.Segments is { Count: > 0 })
        {
            foreach (var seg in payload.Segments)
            {
                var cleaned = seg.Text?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned))
                    continue;

                var relativeMs = (long)Math.Round(Math.Max(0, seg.Start) * 1000.0, MidpointRounding.AwayFromZero);
                results.Add(new TranscriptSegment
                {
                    Id = Guid.NewGuid(),
                    OffsetMs = chunkStartOffsetMs + relativeMs,
                    Text = cleaned,
                    SourceLanguage = detectedLang,
                    CreatedAtUtc = now
                });
            }

            return results;
        }

        var topLevelText = payload.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(topLevelText))
        {
            results.Add(new TranscriptSegment
            {
                Id = Guid.NewGuid(),
                OffsetMs = chunkStartOffsetMs,
                Text = topLevelText,
                SourceLanguage = detectedLang,
                CreatedAtUtc = now
            });
        }

        return results;
    }

    private static string NormalizeLanguageCode(string raw)
    {
        var lower = raw.Trim().ToLowerInvariant();
        return lower switch
        {
            "english" => "en",
            "spanish" => "es",
            "hindi" => "hi",
            "tamil" => "ta",
            "arabic" => "ar",
            "french" => "fr",
            "german" => "de",
            _ when lower.Length >= 2 => lower[..2],
            _ => "en"
        };
    }
}
