using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class UpdateUserProfileCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileCommand request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(request.UserId, ct);
        if (user is null)
        {
            return Result.Failure<UserProfileDto>(
                new Error("User.NotFound", "User account was not found."));
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = UserNormalizationHelper.NormalizeRole(request.Role);
        if (request.Institution is not null)
            user.Institution = UserNormalizationHelper.TrimOrNull(request.Institution);
        if (request.Department is not null)
            user.Department = UserNormalizationHelper.TrimOrNull(request.Department);
        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            user.PreferredLanguage = UserNormalizationHelper.NormalizeLanguage(request.PreferredLanguage);
        if (!string.IsNullOrWhiteSpace(request.AccessibilityMode))
            user.AccessibilityMode = UserNormalizationHelper.NormalizeAccessibilityMode(request.AccessibilityMode);
        if (!string.IsNullOrWhiteSpace(request.CaptionFontSize))
            user.CaptionFontSize = UserNormalizationHelper.NormalizeFontSize(request.CaptionFontSize);
        if (request.AvatarUrl is not null)
            user.AvatarUrl = UserNormalizationHelper.TrimOrNull(request.AvatarUrl);

        await users.SaveChangesAsync(ct);

        return Result.Success(UserProfileDto.FromEntity(user));
    }
}
