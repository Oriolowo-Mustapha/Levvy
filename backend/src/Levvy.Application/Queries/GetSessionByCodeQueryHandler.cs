using Levvy.Application.Common.Interfaces;
using MediatR;

namespace Levvy.Application.Queries;

public sealed class GetSessionByCodeQueryHandler(ISessionRepository repo)
    : IRequestHandler<GetSessionByCodeQuery, SessionSummary?>
{
    public async Task<SessionSummary?> Handle(GetSessionByCodeQuery request, CancellationToken ct)
    {
        var session = await repo.GetByCodeAsync(request.Code, ct);
        if (session is null) return null;

        return new SessionSummary(
            session.Id,
            session.Code,
            session.Title,
            session.EndedAtUtc is null,
            session.EndedAtUtc?.ToString("O"),
            session.Participants.Count);
    }
}
