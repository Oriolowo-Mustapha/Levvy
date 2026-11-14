using MediatR;

namespace Levvy.Application.Commands;

public sealed record JoinSessionCommand(string Code, string DisplayName, string PreferredLanguage) : IRequest<JoinSessionResult>;
public sealed record JoinSessionResult(Guid ParticipantId, Guid SessionId);
