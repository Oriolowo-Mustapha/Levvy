using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class GoogleAuthCommandHandler(
    IUserRepository users,
    IGoogleAuthVerifier googleVerifier,
    IJwtTokenGenerator jwt)
    : IRequestHandler<GoogleAuthCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(GoogleAuthCommand request, CancellationToken ct)
    {
        GoogleUserPayload payload;
        try
        {
            payload = await googleVerifier.VerifyIdTokenAsync(request.IdToken, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or UnauthorizedAccessException)
        {
            return Result.Failure<AuthResponseDto>(
                new Error("Auth.InvalidGoogleToken", ex.Message));
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await users.GetByGoogleSubAsync(payload.GoogleSub, ct)
                   ?? await users.GetByEmailAsync(normalizedEmail, ct);

        var now = DateTime.UtcNow;
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = string.IsNullOrWhiteSpace(payload.FirstName) ? "Levvy" : payload.FirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(payload.LastName) ? "User" : payload.LastName.Trim(),
                Email = normalizedEmail,
                AuthProvider = "google",
                GoogleSub = payload.GoogleSub,
                AvatarUrl = UserNormalizationHelper.TrimOrNull(payload.AvatarUrl),
                Role = UserNormalizationHelper.NormalizeRole(request.Role),
                Institution = UserNormalizationHelper.TrimOrNull(request.Institution),
                Department = UserNormalizationHelper.TrimOrNull(request.Department),
                PreferredLanguage = UserNormalizationHelper.NormalizeLanguage(request.PreferredLanguage),
                AccessibilityMode = UserNormalizationHelper.NormalizeAccessibilityMode(request.AccessibilityMode),
                CaptionFontSize = "md",
                CreatedAtUtc = now,
                LastLoginAtUtc = now
            };

            await users.AddAsync(user, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.GoogleSub))
                user.GoogleSub = payload.GoogleSub;
            if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(payload.AvatarUrl))
                user.AvatarUrl = payload.AvatarUrl.Trim();
            if (string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(payload.FirstName))
                user.FirstName = payload.FirstName.Trim();
            if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(payload.LastName))
                user.LastName = payload.LastName.Trim();

            user.LastLoginAtUtc = now;
        }

        await users.SaveChangesAsync(ct);
        return Result.Success(UserNormalizationHelper.BuildAuthResponse(user, jwt));
    }
}
