namespace Levvy.Application.Common.Interfaces;

public sealed record GoogleUserPayload(
    string GoogleSub,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl);

/// <summary>Verifies Google Identity / OAuth2 ID tokens against Google.</summary>
public interface IGoogleAuthVerifier
{
    Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct);
}
