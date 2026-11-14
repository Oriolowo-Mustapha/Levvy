using Levvy.Application.Common.Interfaces;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Shared helper to resolve the authenticated user ID from a Bearer JWT header (or optional query/route ID).
/// </summary>
internal static class HttpUserResolver
{
    internal static Guid? TryResolveFromBearer(HttpContext http, IJwtTokenGenerator jwt)
    {
        var authHeader = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        return jwt.ValidateTokenAndGetUserId(token);
    }

    internal static Guid? ResolveCurrentUserId(HttpContext http, IJwtTokenGenerator jwt, Guid? explicitUserId = null)
    {
        var fromToken = TryResolveFromBearer(http, jwt);
        if (fromToken is not null)
            return fromToken;

        if (explicitUserId is not null && explicitUserId != Guid.Empty)
            return explicitUserId;

        if (Guid.TryParse(http.Request.Query["userId"], out var fromQuery) && fromQuery != Guid.Empty)
            return fromQuery;

        return null;
    }
}
