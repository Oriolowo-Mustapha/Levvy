using FluentValidation;
using Levvy.Application.Common;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

/// <summary>
/// Attaches a Google Meet / Zoom / Teams URL to an already-created session
/// and schedules the "Levvy Note-Taker" bot dispatch (plan.md Phase 2, Mode A).
/// </summary>
public sealed record ConnectMeetCommand(Guid SessionId, string MeetingUrl)
    : IRequest<ConnectMeetResult>;
public sealed record ConnectMeetResult(Guid SessionId, string Platform, string BotStatus);

public sealed class ConnectMeetCommandValidator : AbstractValidator<ConnectMeetCommand>
{
    public ConnectMeetCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();

        RuleFor(x => x.MeetingUrl)
            .NotEmpty().WithMessage("Meeting URL is required.")
            .Must(url => MeetingUrlParser.TryGetPlatform(url, out _))
            .WithMessage("Meeting URL must be a supported online class link: meet.google.com, zoom.us or teams.microsoft.com.");
    }
}

public sealed class ConnectMeetCommandHandler(
    ISessionRepository repo,
    IBackgroundJobScheduler? scheduler = null)
    : IRequestHandler<ConnectMeetCommand, ConnectMeetResult>
{
    public async Task<ConnectMeetResult> Handle(ConnectMeetCommand request, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(request.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");

        // Already live in-call via the bot: don't spawn a duplicate Note-Taker.
        if (session.BotStatus == "active" && session.ExternalBotId is not null)
            return new ConnectMeetResult(session.Id, session.MeetingPlatform ?? "unknown", session.BotStatus);

        session.MeetingPlatform = MeetingUrlParser.GetPlatform(request.MeetingUrl);
        session.ExternalMeetingUrl = request.MeetingUrl.Trim();
        session.BotStatus = "pending";
        session.ExternalBotId = null;

        await repo.SaveChangesAsync(ct);

        scheduler?.EnqueueDispatchOnlineClassBot(session.Id);

        return new ConnectMeetResult(session.Id, session.MeetingPlatform, session.BotStatus);
    }
}
