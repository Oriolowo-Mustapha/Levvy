using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Authentication;

/// <summary>
/// Lightweight HS256 JWT token generator and validator backed by .NET HMACSHA256.
/// </summary>
public sealed class JwtTokenService(IConfiguration cfg) : IJwtTokenGenerator
{
    private const string DevFallbackSecret = "levvy-dev-only-jwt-secret-change-in-production-32bytes!";

    public (string AccessToken, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTime.UtcNow;
        var expiryMinutes = ParseExpiryMinutes(cfg);
        var expiresAtUtc = now.AddMinutes(expiryMinutes);

        var headerJson = """{"alg":"HS256","typ":"JWT"}""";
        var payloadJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["given_name"] = user.FirstName,
            ["family_name"] = user.LastName,
            ["role"] = user.Role,
            ["lang"] = user.PreferredLanguage,
            ["iss"] = ResolveIssuer(cfg),
            ["aud"] = ResolveAudience(cfg),
            ["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds()
        });

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var signature = ComputeSignature(signingInput, ResolveSecret(cfg));

        return ($"{signingInput}.{signature}", expiresAtUtc);
    }

    public Guid? ValidateTokenAndGetUserId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSignature = ComputeSignature(signingInput, ResolveSecret(cfg));

        var actualBytes = Encoding.UTF8.GetBytes(parts[2]);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        if (!CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
            return null;

        try
        {
            var payloadBytes = Base64UrlDecode(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            if (root.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expUnix))
            {
                if (DateTimeOffset.FromUnixTimeSeconds(expUnix) <= DateTimeOffset.UtcNow)
                    return null;
            }

            if (root.TryGetProperty("iss", out var issProp))
            {
                if (!string.Equals(issProp.GetString(), ResolveIssuer(cfg), StringComparison.Ordinal))
                    return null;
            }

            if (root.TryGetProperty("aud", out var audProp))
            {
                if (!string.Equals(audProp.GetString(), ResolveAudience(cfg), StringComparison.Ordinal))
                    return null;
            }

            if (root.TryGetProperty("sub", out var subProp) &&
                Guid.TryParse(subProp.GetString(), out var userId))
            {
                return userId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveSecret(IConfiguration cfg)
        => cfg["Jwt:Secret"] ?? cfg["JWT_SECRET"] ?? DevFallbackSecret;

    private static string ResolveIssuer(IConfiguration cfg)
        => cfg["Jwt:Issuer"] ?? cfg["JWT_ISSUER"] ?? "levvy-api";

    private static string ResolveAudience(IConfiguration cfg)
        => cfg["Jwt:Audience"] ?? cfg["JWT_AUDIENCE"] ?? "levvy-web";

    private static int ParseExpiryMinutes(IConfiguration cfg)
    {
        var raw = cfg["Jwt:ExpiryMinutes"] ?? cfg["JWT_EXPIRY_MINUTES"];
        return int.TryParse(raw, out var mins) && mins > 0 ? mins : 10080; // 7 days default
    }

    private static string ComputeSignature(string signingInput, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
