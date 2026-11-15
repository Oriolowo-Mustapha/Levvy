using System.Net.Http.Headers;
using System.Net.Http.Json;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Speech;

/// <summary>
/// Primary Text-to-Speech implementation using Deepgram Aura-2 (/v1/speak).
/// </summary>
public class DeepgramTextToSpeech : ITextToSpeech
{
    public const int MaxTextLength = 2000;
    public const string DefaultVoiceModel = "aura-2-thalia-en";

    private static readonly IReadOnlyDictionary<string, string> LanguageVoiceMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "aura-2-thalia-en",
            ["es"] = "aura-2-celeste-es",
            ["fr"] = "aura-2-agathe-fr",
            ["de"] = "aura-2-elara-de",
            ["it"] = "aura-2-livia-it",
            ["ja"] = "aura-2-izanami-ja",
            ["nl"] = "aura-2-beatrix-nl"
        };

    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public DeepgramTextToSpeech(HttpClient http, IConfiguration cfg)
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

    public async Task<TextToSpeechResult> SynthesizeAsync(
        string text,
        string? language = null,
        string? voiceModel = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text to synthesize cannot be empty.", nameof(text));

        var trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
            throw new ArgumentException($"Text exceeds maximum length of {MaxTextLength} characters.", nameof(text));

        var apiKey = _cfg["Deepgram:ApiKey"] ?? _cfg["DEEPGRAM_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("DEEPGRAM_API_KEY is not configured.");

        var model = ResolveVoiceModel(voiceModel, language);
        var query = $"speak?model={Uri.EscapeDataString(model)}&encoding=mp3";

        using var req = new HttpRequestMessage(HttpMethod.Post, query)
        {
            Content = JsonContent.Create(new { text = trimmed })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Deepgram TTS failed with status {(int)resp.StatusCode}: {errBody}",
                null,
                resp.StatusCode);
        }

        var audioBytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
        return new TextToSpeechResult(audioBytes, contentType);
    }

    private string ResolveVoiceModel(string? requestedVoice, string? language)
    {
        if (!string.IsNullOrWhiteSpace(requestedVoice))
            return requestedVoice.Trim();

        if (!string.IsNullOrWhiteSpace(language))
        {
            var primarySubtag = language.Trim().Split('-', '_')[0];
            if (LanguageVoiceMap.TryGetValue(primarySubtag, out var mappedVoice))
                return mappedVoice;
        }

        var configured = _cfg["Deepgram:TtsModel"] ?? _cfg["DEEPGRAM_TTS_MODEL"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        return DefaultVoiceModel;
    }
}
