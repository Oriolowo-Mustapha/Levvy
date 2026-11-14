using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwt)
    : IRequestHandler<RegisterUserCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await users.GetByEmailAsync(normalizedEmail, ct);
        if (existing is not null)
        {
            return Result.Failure<AuthResponseDto>(
                new Error("Auth.EmailExists", "An account with this email already exists."));
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            AuthProvider = "local",
            Role = UserNormalizationHelper.NormalizeRole(request.Role),
            Institution = UserNormalizationHelper.TrimOrNull(request.Institution),
            Department = UserNormalizationHelper.TrimOrNull(request.Department),
            PreferredLanguage = UserNormalizationHelper.NormalizeLanguage(request.PreferredLanguage),
            AccessibilityMode = UserNormalizationHelper.NormalizeAccessibilityMode(request.AccessibilityMode),
            CaptionFontSize = UserNormalizationHelper.NormalizeFontSize(request.CaptionFontSize),
            CreatedAtUtc = now,
            LastLoginAtUtc = now
        };

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        return Result.Success(UserNormalizationHelper.BuildAuthResponse(user, jwt));
    }
}
