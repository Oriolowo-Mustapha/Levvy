using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed record LoginUserCommand(string Email, string Password) : IRequest<Result<AuthResponseDto>>;
