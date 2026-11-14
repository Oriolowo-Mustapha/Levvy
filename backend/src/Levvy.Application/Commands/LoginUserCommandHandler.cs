using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Abstractions;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class LoginUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwt)
    : IRequestHandler<LoginUserCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginUserCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, ct);

        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponseDto>(
                new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);

        return Result.Success(UserNormalizationHelper.BuildAuthResponse(user, jwt));
    }
}
