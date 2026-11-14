using System.Security.Cryptography;
using System.Text;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Audit fix (C2): centralizes "may this request act as the session host?" so the
/// WebSocket role, POST /sessions/{id}/end and POST /sessions/{id}/connect-meet all
/// share one deliberate rule instead of trusting a client-declared query parameter.
///
/// Rule set (in order):
/// 1. A valid Bearer JWT whose subject equals Session.HostId — proper authenticated host.
/// 2. The shared demo host key (Ws:HostKey / WS_HOST_KEY env) via ?host_key= or X-Host-Key —
///    escape hatch for the zero-account demo flow (disabled when the env var is unset).
/// 3. Anonymous demo sessions (HostId is not a registered User account) keep their
///    open-host behaviour from the PRD's "no accounts" era, so the demo never breaks.
/// 4. Otherwise: NOT a host.
/// </summary>
internal static class HostAccess
{
    internal static async Task<bool> IsHostAuthorizedAsync(
        HttpContext http,
        Session session,
        IJwtTokenGenerator jwt,
        IUserRepository users,
        IConfiguration cfg,
        CancellationToken ct)
    {
        // 1. Authenticated owner
        var tokenUserId = HttpUserResolver.TryResolveFromBearer(http, jwt);
        if (tokenUserId is not null && tokenUserId == session.HostId)
            return true;

        // 2. Shared demo host key (query or header)
        var hostKey = cfg["Ws:HostKey"] ?? cfg["WS_HOST_KEY"];
        if (!string.IsNullOrEmpty(hostKey))
        {
            var provided = !string.IsNullOrWhiteSpace(http.Request.Headers["X-Host-Key"].ToString())
                ? http.Request.Headers["X-Host-Key"].ToString()
                : http.Request.Query["host_key"].ToString();
            if (ConstantTimeEquals(provided, hostKey))
                return true;
        }

        // 3. Anonymous demo session: host identity is an unregistered GUID, no credentials exist to demand.
        var hostIsRegisteredUser = await users.GetByIdAsync(session.HostId, ct) is not null;
        if (!hostIsRegisteredUser)
            return true;

        // 4. Registered-user session without valid credentials.
        return false;
    }

    private static bool ConstantTimeEquals(string? a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return false;

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
