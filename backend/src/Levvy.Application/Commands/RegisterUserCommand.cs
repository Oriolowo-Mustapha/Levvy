using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null,
    string? CaptionFontSize = null) : IRequest<Result<AuthResponseDto>>;
