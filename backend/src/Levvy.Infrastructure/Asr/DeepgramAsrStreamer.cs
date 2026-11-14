using System.Net.Http.Headers;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Asr.Models;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Fallback ASR Strategy implementation using Deepgram Nova-2 API.
/// </summary>
public class DeepgramAsrStreamer : IAsrStreamer
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DeepgramAsrStreamer(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
        if (_http.BaseAddress is null)
        {
            var baseUrl = _cfg["Deepgram:BaseUrl"] ?? _cfg["DEEPGRAM_BASE_URL"] ?? "https://api.deepgram.com/v1/";
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
        var apiKey = _cfg["Deepgram:ApiKey"] ?? _cfg["DEEPGRAM_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("DEEPGRAM_API_KEY is not configured.");

        var query = "listen?model=nova-2&smart_format=true&utterances=true";
        if (!string.IsNullOrWhiteSpace(sourceLanguage) &&
            !sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
            !sourceLanguage.Equals("mixed", StringComparison.OrdinalIgnoreCase))
        {
            query += $"&language={Uri.EscapeDataString(sourceLanguage.Trim().ToLowerInvariant())}";
        }
        else
        {
            query += "&detect_language=true";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, query)
        {
            Content = new StreamContent(audioChunk)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        req.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Deepgram ASR failed with status {(int)resp.StatusCode}: {body}",
                null,
                resp.StatusCode);
        }

        return ParseDeepgramResponse(body, chunkStartOffsetMs, sourceLanguage);
    }

    public static IReadOnlyList<TranscriptSegment> ParseDeepgramResponse(
        string json,
        long chunkStartOffsetMs,
        string sourceLanguage = "en")
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TranscriptSegment>();

        var payload = JsonSerializer.Deserialize<DeepgramResponse>(json, JsonOpts);
        if (payload?.Results is null)
            return Array.Empty<TranscriptSegment>();

        var now = DateTime.UtcNow;
        var lang = string.IsNullOrWhiteSpace(sourceLanguage) ? "en" : sourceLanguage;
        var segments = new List<TranscriptSegment>();

        if (payload.Results.Utterances is { Count: > 0 })
        {
            foreach (var u in payload.Results.Utterances)
            {
                var text = u.Transcript?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var relMs = (long)Math.Round(Math.Max(0, u.Start) * 1000.0, MidpointRounding.AwayFromZero);
                segments.Add(new TranscriptSegment
                {
                    Id = Guid.NewGuid(),
                    OffsetMs = chunkStartOffsetMs + relMs,
                    Text = text,
                    SourceLanguage = lang,
                    CreatedAtUtc = now
                });
            }

            return segments;
        }

        var altTranscript = payload.Results.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault()?.Transcript?.Trim();
        if (!string.IsNullOrWhiteSpace(altTranscript))
        {
            segments.Add(new TranscriptSegment
            {
                Id = Guid.NewGuid(),
                OffsetMs = chunkStartOffsetMs,
                Text = altTranscript,
                SourceLanguage = lang,
                CreatedAtUtc = now
            });
        }

        return segments;
    }
}
