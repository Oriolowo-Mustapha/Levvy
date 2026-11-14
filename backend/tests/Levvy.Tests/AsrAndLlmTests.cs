using System.Net;
using System.Text;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Asr;
using Levvy.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Levvy.Tests;

public class AsrAndLlmTests
{
    private static readonly IReadOnlyList<TranscriptSegment> SampleTranscript = new[]
    {
        new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            OffsetMs = 2_507_000, // 41:47
            Text = "Cross-price elasticity measures how quantity demanded of good A responds to a price change in good B.",
            SourceLanguage = "en"
        },
        new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            OffsetMs = 2_592_000, // 43:12
            Text = "When elasticity of substitution is high, consumers switch easily between the two goods.",
            SourceLanguage = "en"
        }
    };

    [Fact]
    public void Whisper_segments_accumulate_chunkStartOffsetMs_into_receipt_timeline()
    {
        var whisperJson = """
        {
          "language": "english",
          "text": "First sentence. Second sentence.",
          "segments": [
            { "start": 0.0, "end": 2.4, "text": " First sentence. " },
            { "start": 2.45, "end": 5.1, "text": " Second sentence." }
          ]
        }
        """;

        var segments = GroqAsrStreamer.ParseWhisperResponse(whisperJson, chunkStartOffsetMs: 60_000, requestedLanguage: "en");

        Assert.Equal(2, segments.Count);
        Assert.Equal(60_000, segments[0].OffsetMs);
        Assert.Equal("First sentence.", segments[0].Text);
        Assert.Equal("en", segments[0].SourceLanguage);

        Assert.Equal(62_450, segments[1].OffsetMs);
        Assert.Equal("Second sentence.", segments[1].Text);
    }

    [Fact]
    public void Grounding_accepts_answer_when_receipts_match_real_transcript_offsets()
    {
        var llmJson = """
        {
          "status": "ok",
          "text": "The professor defined it as how easily consumers switch between two goods when relative prices change.",
          "receipts": [2507000, 2592000]
        }
        """;

        var (status, text, receipts) = GroqLlmClient.ParseAndEnforceGrounding(llmJson, SampleTranscript);

        Assert.Equal("ok", status);
        Assert.Contains("switch between two goods", text);
        Assert.Equal(new long[] { 2_507_000, 2_592_000 }, receipts);
    }

    [Fact]
    public void Grounding_rejects_uncited_or_hallucinated_receipts_and_returns_not_covered()
    {
        // Model claims status="ok" but invents an offset (9999999) that does not exist in the transcript
        var hallucinatedJson = """
        {
          "status": "ok",
          "text": "General knowledge explanation not from the lecture.",
          "receipts": [9999999]
        }
        """;

        var (status, text, receipts) = GroqLlmClient.ParseAndEnforceGrounding(hallucinatedJson, SampleTranscript);

        Assert.Equal("not_covered", status);
        Assert.Contains("43:12", text);
        Assert.Empty(receipts);
    }

    [Fact]
    public async Task LlmClient_falls_back_to_llama_when_primary_model_fails()
    {
        var requestedModels = new List<string>();
        var handler = new FakeHttpMessageHandler(async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            if (body.Contains("gpt-oss-120b"))
            {
                requestedModels.Add("gpt-oss-120b");
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":\"rate_limit\"}")
                };
            }

            requestedModels.Add("llama-3.3-70b-versatile");
            var responseJson = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"status\":\"ok\",\"text\":\"Backed by the fallback model.\",\"receipts\":[2592000]}"
                  }
                }
              ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var cfg = new StubConfiguration(new Dictionary<string, string?>
        {
            ["Groq:ApiKey"] = "test-groq-key",
            ["Groq:AnswerModel"] = "gpt-oss-120b",
            ["Groq:FallbackModel"] = "llama-3.3-70b-versatile"
        });

        var client = new GroqLlmClient(new HttpClient(handler), cfg);
        var (status, text, receipts) = await client.AnswerFromTranscriptAsync(
            "What happens when elasticity of substitution is high?",
            SampleTranscript,
            CancellationToken.None);

        Assert.Equal(new[] { "gpt-oss-120b", "llama-3.3-70b-versatile" }, requestedModels);
        Assert.Equal("ok", status);
        Assert.Equal("Backed by the fallback model.", text);
        Assert.Equal(new long[] { 2_592_000 }, receipts);
    }

    [Fact]
    public async Task ResilientAsrStreamer_falls_back_to_Deepgram_when_Groq_fails()
    {
        var groqHandler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var deepgramHandler = new FakeHttpMessageHandler(_ =>
        {
            var dgJson = """
            {
              "results": {
                "utterances": [
                  { "start": 1.25, "transcript": "Fallback caption from Deepgram." }
                ]
              }
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(dgJson, Encoding.UTF8, "application/json")
            });
        });

        var cfg = new StubConfiguration(new Dictionary<string, string?>
        {
            ["Groq:ApiKey"] = "test-groq-key",
            ["Deepgram:ApiKey"] = "test-dg-key"
        });

        var groq = new GroqAsrStreamer(new HttpClient(groqHandler), cfg);
        var deepgram = new DeepgramAsrStreamer(new HttpClient(deepgramHandler), cfg);
        var resilient = new ResilientAsrStreamer(groq, deepgram, cfg, NullLogger<ResilientAsrStreamer>.Instance);

        using var audio = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var segments = await resilient.TranscribeChunkAsync(audio, chunkStartOffsetMs: 10_000, sourceLanguage: "en", CancellationToken.None);

        Assert.Single(segments);
        Assert.Equal(11_250, segments[0].OffsetMs);
        Assert.Equal("Fallback caption from Deepgram.", segments[0].Text);
    }

    [Fact]
    public void StudyPack_validation_drops_flashcards_without_valid_transcript_receipt()
    {
        var packJson = """
        {
          "summary": "- Elasticity defined [41:47]\n- High substitution [43:12]",
          "flashcards": [
            { "front": "What is cross-price elasticity?", "back": "Response of good A to price of good B", "sourceOffsetMs": 2507000 },
            { "front": "Hallucinated card", "back": "No receipt in lecture", "sourceOffsetMs": 8888888 }
          ]
        }
        """;

        var result = GroqLlmClient.ParseAndValidateStudyPack(packJson, SampleTranscript);

        Assert.Single(result.Flashcards);
        Assert.Equal(2_507_000, result.Flashcards[0].SourceOffsetMs);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request);
    }

    private sealed class StubConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var val) ? val : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }
}
