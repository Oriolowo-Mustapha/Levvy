using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed record UpdateUserProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null,
    string? CaptionFontSize = null,
    string? AvatarUrl = null) : IRequest<Result<UserProfileDto>>;
