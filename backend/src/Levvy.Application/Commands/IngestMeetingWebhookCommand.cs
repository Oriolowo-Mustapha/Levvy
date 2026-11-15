using FluentValidation;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using MediatR;

namespace Levvy.Application.Commands;

/// <summary>
/// Ingests a meeting-bot webhook payload (Recall.ai / MeetingBaas style) for a session:
/// live captions flow into the same <see cref="CaptionFinalized"/> seam as the hot audio pipe,
/// bot lifecycle events update <c>Session.BotStatus</c>, and call end triggers
/// <see cref="EndSessionCommand"/> so the cited Study Pack is generated automatically.
/// </summary>
public sealed record IngestMeetingWebhookCommand(
    Guid SessionId,
    string Type,
    string? Event,
    long? OffsetMs,
    string? Text,
    string? Language,
    string? BotId) : IRequest<IngestMeetingWebhookResult>;
public sealed record IngestMeetingWebhookResult(string Status);

public sealed class IngestMeetingWebhookCommandValidator : AbstractValidator<IngestMeetingWebhookCommand>
{
    public IngestMeetingWebhookCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Caption payloads require text.")
            .When(x => x.Type == "caption");
    }
}

public sealed class IngestMeetingWebhookCommandHandler(
    ISessionRepository repo,
    ISender sender,
    IPublisher publisher)
    : IRequestHandler<IngestMeetingWebhookCommand, IngestMeetingWebhookResult>
{
    public async Task<IngestMeetingWebhookResult> Handle(IngestMeetingWebhookCommand request, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(request.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");

        var normalizedEvent = request.Event?.Trim().ToLowerInvariant();

        switch (request.Type)
        {
            case "caption":
                // Captions are flowing, so the bot is in the call even if we missed the lifecycle event.
                if (session.BotStatus != "active")
                {
                    session.BotStatus = "active";
                    session.ExternalBotId ??= request.BotId;
                    await repo.SaveChangesAsync(ct);
                }

                await publisher.Publish(new CaptionFinalized(
                    session.Id,
                    request.OffsetMs ?? 0,
                    request.Text!,
                    request.Language ?? "en"), ct);
                return new IngestMeetingWebhookResult("caption_accepted");

            case "event" when normalizedEvent is "bot_active" or "in_call" or "in_call_recording"
                or "in_call_not_recording" or "bot.in_call_recording":
                session.BotStatus = "active";
                session.ExternalBotId ??= request.BotId;
                await repo.SaveChangesAsync(ct);
                return new IngestMeetingWebhookResult("ok");

            case "event" when normalizedEvent is "bot_failed" or "fatal" or "failed"
                or "bot.failed" or "recording_permission_denied":
                session.BotStatus = "failed";
                await repo.SaveChangesAsync(ct);
                return new IngestMeetingWebhookResult("ok");

            case "event" when normalizedEvent is "call_ended" or "completed" or "done"
                or "bot.completed" or "complete" or "ended" or "analyzing"
                or "transcribing" or "bot.ended":
                session.BotStatus = "ended";
                await repo.SaveChangesAsync(ct);
                // Ends the session -> SessionEnded -> Hangfire StudyPackGenerationJob (cited notes)
                await sender.Send(new EndSessionCommand(session.Id), ct);
                return new IngestMeetingWebhookResult("ended");

            default:
                return new IngestMeetingWebhookResult("ignored");
        }
    }
}
