using FluentValidation;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Queries;
using Levvy.Domain.Entities;
using MediatR;

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
        group.MapPost("/{id:guid}/end", EndSession).WithSummary("End a live session and queue Study Pack generation");
        group.MapGet("/{code}/pack", GetStudyPack).WithSummary("Get generated Study Pack summary and flashcards");
        group.MapPost("/{id:guid}/audio", UploadAudio).WithSummary("Upload recorded session audio to object storage");
        group.MapGet("/{code}/audio", StreamAudio).WithSummary("Stream recorded session audio with range support");

        return group;
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
        ISender sender,
        CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new JoinSessionCommand(code, req.DisplayName ?? "Student", req.PreferredLanguage ?? "en"),
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
        CancellationToken ct)
    {
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
    }

    private static async Task<IResult> EndSession(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        try
        {
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

    private static async Task<IResult> UploadAudio(
        Guid id,
        HttpRequest request,
        ISessionRepository repo,
        IAudioStore audioStore,
        CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(id, ct);
        if (session is null)
            return Results.NotFound(new { error = "Session not found." });

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
}
