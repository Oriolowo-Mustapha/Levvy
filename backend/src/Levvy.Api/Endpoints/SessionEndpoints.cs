using FluentValidation;
using Levvy.Api.Hubs;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Queries;
using Levvy.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Minimal API route group for /sessions endpoints (dotnet-minimal-api-endpoints skill).
/// </summary>
public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/sessions").WithTags("Sessions");

        group.MapPost("/", CreateSession).WithSummary("Create a new live lecture session");
        group.MapPost("/demo", SeedDemoSession).WithSummary("Seed or retrieve the K4F-8Q2 demo lecture session");
        group.MapGet("/{code}", GetSessionByCode).WithSummary("Get session metadata by room code");
        group.MapPost("/{code}/join", JoinSession).WithSummary("Join a lecture session with preferred subtitle language");
        group.MapGet("/{code}/transcript", GetTranscript).WithSummary("Get full transcript timeline for a session");
        group.MapPost("/{code}/ask", AskQuestion).WithSummary("Ask a grounded question against the lecture transcript");
        group.MapPost("/{code}/translate", TranslateTranscript).WithSummary("Translate existing transcript segments into a target language");
        group.MapPost("/{code}/speak", SpeakText).WithSummary("Synthesize speech audio from text via Deepgram Aura TTS");
        group.MapPost("/{id:guid}/end", EndSession).WithSummary("End a live session and queue Study Pack generation");
        group.MapGet("/{code}/pack", GetStudyPack).WithSummary("Get generated Study Pack summary and flashcards");
        group.MapPost("/{code}/pack/generate", GenerateStudyPackNow).WithSummary("Generate or regenerate the Study Pack and flashcards for a session");
        group.MapPost("/{id:guid}/audio", UploadAudio).WithSummary("Upload recorded session audio to object storage");
        group.MapGet("/{code}/audio", StreamAudio).WithSummary("Stream recorded session audio with range support");

        group.MapPost("/online-class", ConnectOnlineClass).WithSummary("Create a session bound to an online class (Meet/Zoom/Teams) and dispatch the Levvy Note-Taker bot");
        group.MapPost("/{id:guid}/connect-meet", ConnectMeet).WithSummary("Attach a Google Meet / Zoom / Teams URL to an existing session and dispatch the bot");
        group.MapPost("/meet-webhook", MeetWebhookRoot).WithSummary("Account-level Meeting BaaS webhook endpoint (resolves session from payload extra.session_id or acknowledges test pings)");
        group.MapPost("/{id:guid}/meet-webhook", MeetWebhook).WithSummary("Ingest meeting-bot lifecycle events and live captions (call_ended triggers study pack generation)");

        return group;
    }

    private static async Task<IResult> ConnectOnlineClass(
        ConnectOnlineClassRequest req,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new ConnectOnlineClassCommand(req.HostId ?? Guid.NewGuid(), req.Title, req.MeetingUrl),
                ct);
            return Results.Created($"/sessions/{result.Code}", result);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ConnectMeet(
        Guid id,
        ConnectMeetRequest req,
        HttpContext http,
        ISender sender,
        ISessionRepository repo,
        IJwtTokenGenerator jwt,
        IUserRepository users,
        IConfiguration config,
        CancellationToken ct)
    {
        try
        {
            // Audit fix (C2): attaching a bot costs provider money — host only.
            var session = await repo.GetWithTranscriptAsync(id, ct);
            if (session is null)
                return Results.NotFound(new { error = "Session not found." });

            if (!await HostAccess.IsHostAuthorizedAsync(http, session, jwt, users, config, ct))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await sender.Send(new ConnectMeetCommand(id, req.MeetingUrl), ct);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static Task<IResult> MeetWebhook(
        Guid id,
        MeetWebhookRequest req,
        HttpRequest http,
        IConfiguration config,
        ISender sender,
        LectureSessionHub hub,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!IsWebhookAuthorized(http, config))
            return Task.FromResult(Results.Unauthorized());

        return DispatchWebhookCommandAsync(id, req, sender, hub, loggerFactory, ct);
    }

    private static Task<IResult> MeetWebhookRoot(
        MeetWebhookRequest req,
        HttpRequest http,
        IConfiguration config,
        ISender sender,
        LectureSessionHub hub,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!IsWebhookAuthorized(http, config))
            return Task.FromResult(Results.Unauthorized());

        var sessionId = ResolveWebhookSessionId(req);
        if (sessionId is null)
            return Task.FromResult(Results.Ok(new { status = "webhook_verified" }));

        return DispatchWebhookCommandAsync(sessionId.Value, req, sender, hub, loggerFactory, ct);
    }

    private static bool IsWebhookAuthorized(HttpRequest http, IConfiguration config)
    {
        // Shared-secret check when MEETING_BOT_WEBHOOK_SECRET is configured (supports Meeting BaaS & Levvy headers).
        var expectedSecret = config["MeetingBot:WebhookSecret"] ?? config["MEETING_BOT_WEBHOOK_SECRET"];
        if (string.IsNullOrEmpty(expectedSecret))
            return true;

        var providedSecret = http.Headers["X-Meeting-Webhook-Secret"].FirstOrDefault()
            ?? http.Headers["x-meeting-baas-api-key"].FirstOrDefault()
            ?? http.Headers["x-mb-secret"].FirstOrDefault();
        return string.Equals(providedSecret, expectedSecret, StringComparison.Ordinal);
    }

    private static async Task<IResult> DispatchWebhookCommandAsync(
        Guid sessionId,
        MeetWebhookRequest req,
        ISender sender,
        LectureSessionHub hub,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Levvy.Api.MeetWebhook");
        try
        {
            var (type, eventName, botId, offsetMs, text, language) = NormalizeMeetWebhook(req);
            logger.LogInformation(
                "Received meet webhook for session {SessionId}: type={Type}, event={Event}, botId={BotId}, hasText={HasText}",
                sessionId, type, eventName, botId, !string.IsNullOrWhiteSpace(text));

            var result = await sender.Send(
                new IngestMeetingWebhookCommand(sessionId, type, eventName, offsetMs, text, language, botId),
                ct);

            if (result.Status == "caption_accepted" && !string.IsNullOrWhiteSpace(text))
            {
                await hub.BroadcastExternalCaptionAsync(
                    sessionId,
                    offsetMs ?? 0,
                    text,
                    language ?? "en",
                    ct);
            }

            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static Guid? ResolveWebhookSessionId(MeetWebhookRequest req)
    {
        if (req.SessionId is { } explicitId && explicitId != Guid.Empty)
            return explicitId;

        if (req.Data is not { ValueKind: System.Text.Json.JsonValueKind.Object } dataEl)
            return null;

        if (TryGetJsonGuid(dataEl, "extra", "session_id") is { } extraId)
            return extraId;

        if (TryGetJsonGuid(dataEl, "metadata", "session_id") is { } metadataId)
            return metadataId;

        return TryGetJsonGuid(dataEl, "session_id");
    }

    private static Guid? TryGetJsonGuid(System.Text.Json.JsonElement obj, string propertyName)
    {
        var raw = TryGetJsonString(obj, propertyName);
        return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty ? parsed : null;
    }

    private static Guid? TryGetJsonGuid(System.Text.Json.JsonElement obj, string nestedObjectProperty, string propertyName)
    {
        if (!obj.TryGetProperty(nestedObjectProperty, out var nestedEl) ||
            nestedEl.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        return TryGetJsonGuid(nestedEl, propertyName);
    }

    private static (string Type, string? Event, string? BotId, long? OffsetMs, string? Text, string? Language) NormalizeMeetWebhook(MeetWebhookRequest req)
    {
        var type = string.IsNullOrWhiteSpace(req.Type) && !string.IsNullOrWhiteSpace(req.Event)
            ? "event"
            : (req.Type ?? string.Empty);

        var botId = req.BotId;
        var eventName = req.Event;
        var offsetMs = req.OffsetMs;
        var text = req.Text;
        var language = req.Language;

        if (req.Data is { ValueKind: System.Text.Json.JsonValueKind.Object } dataEl)
        {
            if (string.IsNullOrWhiteSpace(botId))
            {
                botId = TryGetJsonString(dataEl, "bot_id") ?? TryGetJsonString(dataEl, "id");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = TryGetJsonString(dataEl, "text") ?? TryGetJsonString(dataEl, "transcript");
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                language = TryGetJsonString(dataEl, "language") ?? TryGetJsonString(dataEl, "language_code");
            }

            if (offsetMs is null)
            {
                if (dataEl.TryGetProperty("offset_ms", out var offsetEl) && offsetEl.TryGetInt64(out var parsedMs))
                {
                    offsetMs = parsedMs;
                }
                else if (dataEl.TryGetProperty("start_time", out var startEl) && startEl.TryGetDouble(out var startSec))
                {
                    offsetMs = (long)Math.Round(Math.Max(0, startSec) * 1000.0);
                }
            }

            if ((eventName?.Contains("transcript", StringComparison.OrdinalIgnoreCase) == true) &&
                !string.IsNullOrWhiteSpace(text))
            {
                var isFinal = !dataEl.TryGetProperty("is_final", out var finalEl) ||
                              finalEl.ValueKind != System.Text.Json.JsonValueKind.False;
                if (isFinal)
                    type = "caption";
            }

            if (dataEl.TryGetProperty("status", out var statusEl))
            {
                string? nestedStatus = statusEl.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => statusEl.GetString(),
                    System.Text.Json.JsonValueKind.Object => TryGetJsonString(statusEl, "code"),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(nestedStatus))
                {
                    eventName = nestedStatus;
                }
            }
        }

        return (type, eventName, botId, offsetMs, text, language);
    }

    private static string? TryGetJsonString(System.Text.Json.JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != System.Text.Json.JsonValueKind.String)
            return null;

        var value = el.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest req,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new CreateSessionCommand(req.Title, req.HostId ?? Guid.NewGuid()), ct);
            return Results.Created($"/sessions/{result.Code}", result);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetSessionByCode(
        string code,
        ISender sender,
        CancellationToken ct)
    {
        var summary = await sender.Send(new GetSessionByCodeQuery(code), ct);
        return summary is not null ? Results.Ok(summary) : Results.NotFound(new { error = "Session not found." });
    }

    private static async Task<IResult> JoinSession(
        string code,
        JoinSessionRequest req,
        HttpContext http,
        IJwtTokenGenerator jwt,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var userId = HttpUserResolver.ResolveCurrentUserId(http, jwt, req.UserId);
            var result = await sender.Send(
                new JoinSessionCommand(code, req.DisplayName ?? "Student", req.PreferredLanguage ?? "en", userId),
                ct);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetTranscript(
        string code,
        ISender sender,
        CancellationToken ct)
    {
        var session = await sender.Send(new GetSessionByCodeQuery(code), ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        var lines = await sender.Send(new GetTranscriptQuery(session.Id), ct);
        return Results.Ok(new
        {
            session.Id,
            session.Code,
            session.Title,
            session.IsLive,
            session.ParticipantCount,
            lines
        });
    }

    private static async Task<IResult> AskQuestion(
        string code,
        AskQuestionRequest req,
        ISender sender,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Levvy.Api.AskQuestion");
        try
        {
            var result = await sender.Send(new AskQuestionCommand(code, req.AskerId ?? Guid.NewGuid(), req.Text), ct);
            return Results.Ok(new
            {
                type = "answer",
                status = result.Status,
                text = result.Text,
                receipts = result.ReceiptOffsetsMs.Select(ms => ms / 1000).ToArray(),
                receiptOffsetsMs = result.ReceiptOffsetsMs
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "AskQuestion configuration error for session {Code}", code);
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "AskQuestion upstream LLM error for session {Code}", code);
            return Results.Json(new { error = $"AI service error: {ex.Message}" }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AskQuestion unexpected failure for session {Code}", code);
            return Results.Json(new { error = $"Failed to answer question: {ex.Message}" }, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> TranslateTranscript(
        string code,
        TranslateTranscriptRequest req,
        ISender sender,
        ITranslator translator,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var targetLanguage = req.TargetLanguage?.Trim();
        if (string.IsNullOrWhiteSpace(targetLanguage) ||
            string.Equals(targetLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "A valid targetLanguage is required." });
        }

        var session = await sender.Send(new GetSessionByCodeQuery(code), ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        var lines = await sender.Send(new GetTranscriptQuery(session.Id), ct);
        var filterOffsets = req.OffsetsMs is { Count: > 0 }
            ? new HashSet<long>(req.OffsetsMs)
            : null;

        var toTranslate = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Text) &&
                        (filterOffsets is null || filterOffsets.Contains(l.OffsetMs)))
            .Take(60)
            .ToList();

        if (toTranslate.Count == 0)
            return Results.Ok(new { targetLanguage, translations = Array.Empty<object>() });

        var logger = loggerFactory.CreateLogger("Levvy.Api.TranslateTranscript");
        try
        {
            var results = await Task.WhenAll(toTranslate.Select(async line =>
            {
                var translated = await translator.TranslateAsync(line.Text, targetLanguage, ct);
                return new { offsetMs = line.OffsetMs, text = translated };
            }));

            return Results.Ok(new { targetLanguage, translations = results });
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "TranslateTranscript failed for session {Code} -> {Lang}", code, targetLanguage);
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> SpeakText(
        string code,
        SpeakTextRequest req,
        ISender sender,
        ITextToSpeech tts,
        CancellationToken ct)
    {
        var session = await sender.Send(new GetSessionByCodeQuery(code), ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        try
        {
            var audio = await tts.SynthesizeAsync(req.Text, req.Language, req.VoiceModel, ct);
            return Results.File(audio.AudioBytes, audio.ContentType);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> EndSession(
        Guid id,
        HttpContext http,
        ISender sender,
        ISessionRepository repo,
        IJwtTokenGenerator jwt,
        IUserRepository users,
        IConfiguration config,
        CancellationToken ct)
    {
        try
        {
            // Audit fix (C2): ending a session is destructive (kills live captions + LLM spend) — host only.
            var session = await repo.GetWithTranscriptAsync(id, ct);
            if (session is null)
                return Results.NotFound(new { error = "Session not found." });

            if (!await HostAccess.IsHostAuthorizedAsync(http, session, jwt, users, config, ct))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await sender.Send(new EndSessionCommand(id), ct);
            return Results.Ok(new { status = "ended", sessionId = id });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetStudyPack(
        string code,
        ISender sender,
        CancellationToken ct)
    {
        var pack = await sender.Send(new GetStudyPackByCodeQuery(code), ct);
        return pack is not null ? Results.Ok(pack) : Results.NotFound(new { error = "Session not found." });
    }

    private static async Task<IResult> GenerateStudyPackNow(
        string code,
        ISender sender,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var session = await sender.Send(new GetSessionByCodeQuery(code), ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        var logger = loggerFactory.CreateLogger("Levvy.Api.GenerateStudyPackNow");
        try
        {
            await sender.Send(new GenerateStudyPackCommand(session.Id), ct);
            var pack = await sender.Send(new GetStudyPackByCodeQuery(code), ct);
            return pack is not null ? Results.Ok(pack) : Results.NotFound(new { error = "Study pack not found." });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "GenerateStudyPackNow configuration error for session {Code}", code);
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "GenerateStudyPackNow upstream LLM error for session {Code}", code);
            return Results.Json(new { error = $"AI service error: {ex.Message}" }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> UploadAudio(
        Guid id,
        HttpRequest request,
        HttpContext http,
        IJwtTokenGenerator jwt,
        IUserRepository users,
        IConfiguration config,
        ISessionRepository repo,
        IAudioStore audioStore,
        CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(id, ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

        if (!await HostAccess.IsHostAuthorizedAsync(http, session, jwt, users, config, ct))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var key = await audioStore.SaveRecordingAsync(id, request.Body, ct);
        session.RecordingKey = key;
        await repo.SaveChangesAsync(ct);

        return Results.Ok(new { sessionId = id, recordingKey = key });
    }

    private static async Task<IResult> StreamAudio(
        string code,
        ISessionRepository repo,
        IAudioStore audioStore,
        CancellationToken ct)
    {
        var session = await repo.GetByCodeAsync(code, ct);
        if (session is null || string.IsNullOrWhiteSpace(session.RecordingKey))
            return Results.NotFound(new { error = "Recording not found." });

        var stream = await audioStore.OpenRecordingAsync(session.RecordingKey, ct);
        return stream is not null
            ? Results.Stream(stream, "audio/webm", enableRangeProcessing: true)
            : Results.NotFound(new { error = "Recording file unavailable." });
    }

    private static async Task<IResult> SeedDemoSession(ISessionRepository repo, CancellationToken ct)
    {
        const string demoCode = "K4F-8Q2";
        var existing = await repo.GetByCodeAsync(demoCode, ct);
        if (existing is not null)
            return Results.Ok(new CreateSessionResult(existing.Id, existing.Code));

        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var session = new Session
        {
            Id = sessionId,
            Code = demoCode,
            Title = "ECN-201 · Elasticity of demand — Lecture 7",
            HostId = Guid.NewGuid(),
            StartedAtUtc = now,
            Segments = new List<TranscriptSegment>
            {
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_412_000, Text = "Before we move on: on the price elasticity homework, most of you found demand inelastic.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_458_000, Text = "Let's take the cross elasticity of demand as our second case.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_481_000, Text = "Two goods are substitutes when one can stand in for the other.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_507_000, Text = "If the price of coffee climbs, demand for tea rises with it.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_528_000, Text = "Complements behave the other way around...", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_556_000, Text = "printers and ink, cars and petrol.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_579_000, Text = "Negative cross elasticity is a signal that goods are consumed together.", SourceLanguage = "en", CreatedAtUtc = now },
                new() { Id = Guid.NewGuid(), SessionId = sessionId, OffsetMs = 2_592_000, Text = "So the sign of the cross elasticity tells you which relationship you're looking at.", SourceLanguage = "en", CreatedAtUtc = now }
            },
            StudyPack = new StudyPack
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Summary = "• Price elasticity homework recap: most students found demand inelastic [40:12]\n• Cross elasticity of demand measures how quantity demanded of one good responds to price changes in another [40:58]\n• Substitutes (e.g., coffee and tea) exhibit positive cross elasticity [41:47]\n• Complements (e.g., printers and ink, cars and petrol) exhibit negative cross elasticity [42:36]\n• The sign (+ or −) directly identifies the relationship between the two goods [43:12]",
                GeneratedAtUtc = now,
                Flashcards = new List<Flashcard>
                {
                    new() { Id = Guid.NewGuid(), Front = "What does positive cross elasticity indicate about two goods?", Back = "They are substitutes — e.g., when the price of coffee climbs, demand for tea rises.", SourceOffsetMs = 2_507_000 },
                    new() { Id = Guid.NewGuid(), Front = "What is the cross elasticity sign for complements like printers and ink?", Back = "Negative — signaling the goods are consumed together.", SourceOffsetMs = 2_556_000 },
                    new() { Id = Guid.NewGuid(), Front = "What single property of cross elasticity tells you the relationship between two goods?", Back = "Its sign (positive for substitutes, negative for complements).", SourceOffsetMs = 2_592_000 }
                }
            }
        };

        await repo.AddAsync(session, ct);
        await repo.SaveChangesAsync(ct);

        return Results.Ok(new CreateSessionResult(session.Id, session.Code));
    }

    public sealed record TranslateTranscriptRequest(string TargetLanguage, IReadOnlyList<long>? OffsetsMs = null);
}
