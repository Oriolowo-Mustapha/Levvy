using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Llm.Models;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Llm;

/// <summary>
/// Groq chat completions client (OpenAI-compatible endpoint) implementing ITranslator and ILlmAnswerer.
/// Automatically falls back from GROQ_ANSWER_MODEL (gpt-oss-120b) to GROQ_FALLBACK_MODEL (llama-3.3-70b-versatile).
/// </summary>
public class GroqLlmClient : ITranslator, ILlmAnswerer
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GroqLlmClient(HttpClient http, IConfiguration cfg)
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

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(targetLanguage))
            return text.Trim();

        var systemPrompt =
            $"You are Levvy's live lecture subtitle translator. Translate the professor's spoken sentence into '{targetLanguage}'. " +
            "Keep technical and academic terminology accurate, preserve the natural spoken flow, and output ONLY the translated text with no quotes or commentary.";

        var content = await CompleteWithFallbackAsync(
            systemPrompt,
            text.Trim(),
            jsonMode: false,
            temperature: 0.1,
            ct);

        return string.IsNullOrWhiteSpace(content) ? text.Trim() : content.Trim();
    }

    public async Task<(string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs)> AnswerFromTranscriptAsync(
        string question,
        IReadOnlyList<TranscriptSegment> transcriptSoFar,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be empty.", nameof(question));

        if (transcriptSoFar is null || transcriptSoFar.Count == 0)
        {
            return (
                "not_covered",
                "The lecture has just started (00:00). This topic hasn't come up yet — I'll flag it if the lecturer covers it.",
                Array.Empty<long>());
        }

        var ordered = transcriptSoFar.OrderBy(s => s.OffsetMs).ToList();
        var latestTs = TimestampFormatter.FormatMmSs(ordered[^1].OffsetMs);
        var transcriptBlock = BuildTranscriptPromptBlock(ordered);

        var systemPrompt =
            "You are Ask Levvy, the grounded mid-lecture Q&A assistant. " +
            "NON-NEGOTIABLE RULE: Cite or say 'not_covered'. " +
            "You may ONLY answer using facts explicitly stated in the provided lecture transcript segments. " +
            "NEVER use outside or general knowledge. " +
            "Return a strict JSON object with exactly these keys:\n" +
            "{\n" +
            "  \"status\": \"ok\" | \"not_covered\",\n" +
            "  \"text\": \"concise explanation grounded in the professor's words (or honest message stating the lecturer is at " + latestTs + " and hasn't covered it yet)\",\n" +
            "  \"receipts\": [integer OffsetMs values copied directly from the [offsetMs] tags in the transcript]\n" +
            "}\n" +
            "If the topic has not been covered yet in the transcript, you MUST set \"status\": \"not_covered\" and \"receipts\": [].";

        var userPrompt =
            $"LECTURE TRANSCRIPT SO FAR (latest timestamp {latestTs}):\n{transcriptBlock}\n\n" +
            $"STUDENT QUESTION:\n{question.Trim()}";

        var rawJson = await CompleteWithFallbackAsync(
            systemPrompt,
            userPrompt,
            jsonMode: true,
            temperature: 0.0,
            ct);

        return GroundingVerifier.ParseAndEnforceGrounding(rawJson, ordered, latestTs);
    }

    public async Task<GeneratedStudyPackResult> GenerateStudyPackAsync(
        IReadOnlyList<TranscriptSegment> transcript,
        CancellationToken ct)
    {
        if (transcript is null || transcript.Count == 0)
        {
            return new GeneratedStudyPackResult(
                "No lecture transcript segments were recorded for this session.",
                Array.Empty<GeneratedFlashcardItem>());
        }

        var ordered = transcript.OrderBy(s => s.OffsetMs).ToList();
        var transcriptBlock = BuildTranscriptPromptBlock(ordered);

        var systemPrompt =
            "You generate Levvy's after-class grounded Study Pack from a live lecture transcript. " +
            "NON-NEGOTIABLE RULE: Every key point in the summary and every flashcard MUST come strictly from the transcript and cite a real [offsetMs] tag from the transcript. " +
            "Never add external facts. Return a strict JSON object:\n" +
            "{\n" +
            "  \"summary\": \"Clear bulleted summary where each bullet includes its [mm:ss] timestamp citation\",\n" +
            "  \"flashcards\": [\n" +
            "    { \"front\": \"Question\", \"back\": \"Grounded answer\", \"sourceOffsetMs\": 2507000 }\n" +
            "  ]\n" +
            "}";

        var rawJson = await CompleteWithFallbackAsync(
            systemPrompt,
            $"FULL LECTURE TRANSCRIPT:\n{transcriptBlock}",
            jsonMode: true,
            temperature: 0.1,
            ct);

        return GroundingVerifier.ParseAndValidateStudyPack(rawJson, ordered);
    }

    // Backwards-compatible static forwarding methods for existing call sites/tests
    public static (string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs) ParseAndEnforceGrounding(
        string rawJson,
        IReadOnlyList<TranscriptSegment> transcriptSoFar,
        string? latestTsOverride = null)
        => GroundingVerifier.ParseAndEnforceGrounding(rawJson, transcriptSoFar, latestTsOverride);

    public static GeneratedStudyPackResult ParseAndValidateStudyPack(
        string rawJson,
        IReadOnlyList<TranscriptSegment> transcript)
        => GroundingVerifier.ParseAndValidateStudyPack(rawJson, transcript);

    public static string FormatMmSs(long offsetMs) => TimestampFormatter.FormatMmSs(offsetMs);

    private static string BuildTranscriptPromptBlock(IReadOnlyList<TranscriptSegment> ordered)
    {
        var sb = new StringBuilder(ordered.Count * 96);
        foreach (var seg in ordered)
        {
            sb.Append('[')
              .Append(seg.OffsetMs)
              .Append("] (")
              .Append(TimestampFormatter.FormatMmSs(seg.OffsetMs))
              .Append(") ")
              .AppendLine(seg.Text);
        }
        return sb.ToString();
    }

    private async Task<string> CompleteWithFallbackAsync(
        string systemPrompt,
        string userPrompt,
        bool jsonMode,
        double temperature,
        CancellationToken ct)
    {
        var apiKey = _cfg["Groq:ApiKey"] ?? _cfg["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("GROQ_API_KEY is not configured in .env or environment variables.");

        var primaryModel = NormalizeGroqModel(
            _cfg["Groq:AnswerModel"] ?? _cfg["GROQ_ANSWER_MODEL"],
            "openai/gpt-oss-120b");
        var fallbackModel = NormalizeGroqModel(
            _cfg["Groq:FallbackModel"] ?? _cfg["GROQ_FALLBACK_MODEL"],
            "openai/gpt-oss-20b");

        try
        {
            return await SendChatCompletionAsync(apiKey, primaryModel, systemPrompt, userPrompt, jsonMode, temperature, ct);
        }
        // Audit fix (M3): fail over on HTTP errors AND on client-side timeouts (TaskCanceledException
        // raised by the HttpClient 30s timeout, which is not a caller cancellation).
        catch (Exception ex) when (
            (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested)) &&
            !string.Equals(primaryModel, fallbackModel, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return await SendChatCompletionAsync(apiKey, fallbackModel, systemPrompt, userPrompt, jsonMode, temperature, ct);
            }
            catch (HttpRequestException ex2) when (
                ex2.StatusCode == System.Net.HttpStatusCode.NotFound &&
                !string.Equals(fallbackModel, "openai/gpt-oss-20b", StringComparison.OrdinalIgnoreCase))
            {
                return await SendChatCompletionAsync(apiKey, "openai/gpt-oss-20b", systemPrompt, userPrompt, jsonMode, temperature, ct);
            }
        }
    }

    private static string NormalizeGroqModel(string? configured, string defaultModel)
    {
        var model = configured?.Trim();
        if (string.IsNullOrWhiteSpace(model))
            return defaultModel;

        return model switch
        {
            "gpt-oss-120b" => "openai/gpt-oss-120b",
            "gpt-oss-20b" => "openai/gpt-oss-20b",
            _ => model
        };
    }

    private async Task<string> SendChatCompletionAsync(
        string apiKey,
        string model,
        string systemPrompt,
        string userPrompt,
        bool jsonMode,
        double temperature,
        CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        if (jsonMode)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Groq chat completion ({model}) failed with status {(int)resp.StatusCode}: {body}",
                null,
                resp.StatusCode);
        }

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOpts);
        return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }
}
