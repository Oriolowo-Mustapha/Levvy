using FluentValidation;
using Levvy.Application.Common;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

/// <summary>
/// Creates a live session bound to an online class URL (Google Meet / Zoom / Teams)
/// and schedules the "Levvy Note-Taker" bot dispatch (plan.md Phase 2, Mode A).
/// </summary>
public sealed record ConnectOnlineClassCommand(Guid HostId, string Title, string MeetingUrl)
    : IRequest<ConnectOnlineClassResult>;
public sealed record ConnectOnlineClassResult(Guid SessionId, string Code, string Platform, string BotStatus);

public sealed class ConnectOnlineClassCommandValidator : AbstractValidator<ConnectOnlineClassCommand>
{
    public ConnectOnlineClassCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Session title is required.")
            .MaximumLength(200);

        RuleFor(x => x.MeetingUrl)
            .NotEmpty().WithMessage("Meeting URL is required.")
            .Must(url => MeetingUrlParser.TryGetPlatform(url, out _))
            .WithMessage("Meeting URL must be a supported online class link: meet.google.com, zoom.us or teams.microsoft.com.");
    }
}

public sealed class ConnectOnlineClassCommandHandler(
    ISessionRepository repo,
    IBackgroundJobScheduler? scheduler = null)
    : IRequestHandler<ConnectOnlineClassCommand, ConnectOnlineClassResult>
{
    public async Task<ConnectOnlineClassResult> Handle(ConnectOnlineClassCommand request, CancellationToken ct)
    {
        var platform = MeetingUrlParser.GetPlatform(request.MeetingUrl); // defence in depth behind the validator

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = SessionCodeGenerator.Generate(),
            Title = request.Title.Trim(),
            HostId = request.HostId == Guid.Empty ? Guid.NewGuid() : request.HostId,
            StartedAtUtc = DateTime.UtcNow,
            MeetingPlatform = platform,
            ExternalMeetingUrl = request.MeetingUrl.Trim(),
            BotStatus = "pending"
        };

        await repo.AddAsync(session, ct);
        await repo.SaveChangesAsync(ct);

        // Hangfire worker dispatches the bot (no API key -> connector reports "unconfigured", Mode B fallback)
        scheduler?.EnqueueDispatchOnlineClassBot(session.Id);

        return new ConnectOnlineClassResult(session.Id, session.Code, session.MeetingPlatform, session.BotStatus);
    }
}
