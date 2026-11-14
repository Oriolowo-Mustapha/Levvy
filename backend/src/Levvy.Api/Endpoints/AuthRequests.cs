namespace Levvy.Api.Endpoints;

public sealed record RegisterUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null,
    string? CaptionFontSize = null);

public sealed record LoginUserRequest(string Email, string Password);

public sealed record GoogleAuthRequest(
    string IdToken,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null);

public sealed record UpdateUserProfileRequest(
    string FirstName,
    string LastName,
    string? Role = null,
    string? Institution = null,
    string? Department = null,
    string? PreferredLanguage = null,
    string? AccessibilityMode = null,
    string? CaptionFontSize = null,
    string? AvatarUrl = null);
