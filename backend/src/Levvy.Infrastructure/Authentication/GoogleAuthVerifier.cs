using System.Text.Json;
using System.Text.Json.Serialization;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Authentication;

/// <summary>
/// Verifies Google Sign-In ID tokens via Google's OAuth2 tokeninfo endpoint.
/// </summary>
public sealed class GoogleAuthVerifier(HttpClient http, IConfiguration cfg) : IGoogleAuthVerifier
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);

        var url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
        using var resp = await http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException($"Google token verification failed ({(int)resp.StatusCode}).");
        }

        var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfoResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException("Empty response from Google token verification.");

        if (string.IsNullOrWhiteSpace(tokenInfo.Sub) || string.IsNullOrWhiteSpace(tokenInfo.Email))
        {
            throw new InvalidOperationException("Google token did not include required sub or email claims.");
        }

        if (string.Equals(tokenInfo.EmailVerified, "false", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Google account email is not verified.");
        }

        var configuredClientId = cfg["Google:ClientId"] ?? cfg["GOOGLE_CLIENT_ID"];
        if (!string.IsNullOrWhiteSpace(configuredClientId) &&
            !string.Equals(tokenInfo.Aud, configuredClientId.Trim(), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Google token audience does not match GOOGLE_CLIENT_ID.");
        }

        var (firstName, lastName) = ResolveNames(tokenInfo);
        return new GoogleUserPayload(
            tokenInfo.Sub.Trim(),
            tokenInfo.Email.Trim().ToLowerInvariant(),
            firstName,
            lastName,
            string.IsNullOrWhiteSpace(tokenInfo.Picture) ? null : tokenInfo.Picture.Trim());
    }

    private static (string FirstName, string LastName) ResolveNames(GoogleTokenInfoResponse info)
    {
        if (!string.IsNullOrWhiteSpace(info.GivenName))
        {
            return (info.GivenName.Trim(), string.IsNullOrWhiteSpace(info.FamilyName) ? "User" : info.FamilyName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(info.Name))
        {
            var parts = info.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? (parts[0], parts[1]) : (parts[0], "User");
        }

        return ("Levvy", "User");
    }

    private sealed class GoogleTokenInfoResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("aud")]
        public string? Aud { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public string? EmailVerified { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }
}
