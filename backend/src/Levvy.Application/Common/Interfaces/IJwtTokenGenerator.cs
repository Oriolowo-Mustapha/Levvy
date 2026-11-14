using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

/// <summary>JWT Bearer token generator and validator.</summary>
public interface IJwtTokenGenerator
{
    (string AccessToken, DateTime ExpiresAtUtc) GenerateToken(User user);
    Guid? ValidateTokenAndGetUserId(string token);
}
