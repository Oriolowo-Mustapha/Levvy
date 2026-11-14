using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed record GoogleAuthCommand(
    string IdToken,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null) : IRequest<Result<AuthResponseDto>>;
