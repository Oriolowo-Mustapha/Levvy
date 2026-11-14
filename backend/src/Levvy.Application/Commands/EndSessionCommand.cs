using MediatR;

namespace Levvy.Application.Commands;

public sealed record EndSessionCommand(Guid SessionId) : IRequest;
