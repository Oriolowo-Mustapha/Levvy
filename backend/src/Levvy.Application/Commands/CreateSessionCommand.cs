using MediatR;

namespace Levvy.Application.Commands;

public sealed record CreateSessionCommand(string Title, Guid HostId) : IRequest<CreateSessionResult>;
public sealed record CreateSessionResult(Guid SessionId, string Code);
