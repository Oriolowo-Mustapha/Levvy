using MediatR;

namespace Levvy.Application.Queries;

public sealed record GetSessionByCodeQuery(string Code) : IRequest<SessionSummary?>;
public sealed record SessionSummary(Guid Id, string Code, string Title, bool IsLive, string? EndedAtUtc, int ParticipantCount = 0);
