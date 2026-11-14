using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;

namespace Levvy.Application.Commands;

/// <summary>
/// Shared normalization and auth response builder for user registration, Google SSO, and profile updates.
/// </summary>
internal static class UserNormalizationHelper
{
    internal static readonly string[] AllowedRoles = ["student", "instructor"];
    internal static readonly string[] AllowedAccessibilityModes = ["standard", "deaf_hoh", "international"];
    internal static readonly string[] AllowedFontSizes = ["sm", "md", "lg", "xl"];

    internal static string NormalizeRole(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized is not null && AllowedRoles.Contains(normalized) ? normalized : "student";
    }

    internal static string NormalizeAccessibilityMode(string? mode)
    {
        var normalized = mode?.Trim().ToLowerInvariant();
        return normalized is not null && AllowedAccessibilityModes.Contains(normalized) ? normalized : "standard";
    }

    internal static string NormalizeFontSize(string? size)
    {
        var normalized = size?.Trim().ToLowerInvariant();
        return normalized is not null && AllowedFontSizes.Contains(normalized) ? normalized : "md";
    }

    internal static string NormalizeLanguage(string? lang)
        => string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();

    internal static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static AuthResponseDto BuildAuthResponse(User user, IJwtTokenGenerator jwt)
    {
        var (token, expiresAtUtc) = jwt.GenerateToken(user);
        return new AuthResponseDto(token, expiresAtUtc, UserProfileDto.FromEntity(user));
    }
}
