using MediatR;

namespace Levvy.Application.Commands;

public sealed record GenerateStudyPackCommand(Guid SessionId) : IRequest<Guid>;
