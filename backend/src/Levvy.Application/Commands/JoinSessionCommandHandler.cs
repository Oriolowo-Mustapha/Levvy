using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class JoinSessionCommandHandler(ISessionRepository repo)
    : IRequestHandler<JoinSessionCommand, JoinSessionResult>
{
    public async Task<JoinSessionResult> Handle(JoinSessionCommand request, CancellationToken ct)
    {
        var session = await repo.GetByCodeAsync(request.Code, ct)
            ?? throw new KeyNotFoundException($"Session '{request.Code}' was not found.");

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserId = request.UserId,
            DisplayName = request.DisplayName.Trim(),
            PreferredLanguage = request.PreferredLanguage.Trim().ToLowerInvariant(),
            JoinedAtUtc = DateTime.UtcNow
        };

        session.Participants.Add(participant);
        await repo.SaveChangesAsync(ct);

        return new JoinSessionResult(participant.Id, session.Id);
    }
}
