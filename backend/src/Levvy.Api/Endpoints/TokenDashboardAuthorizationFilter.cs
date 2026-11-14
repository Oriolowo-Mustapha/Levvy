using System.Net;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;

namespace Levvy.Api.Endpoints;

/// <summary>
/// Audit fix (H4): Hangfire's default LocalRequestsOnlyAuthorizationFilter is an accident
/// behind a reverse proxy (nothing appears local, or worse — everything does when nginx
/// runs on the same box). This filter makes dashboard access deliberate:
///   1. A request bearing the configured dashboard token (X-Dashboard-Token header or
///      ?dashboard_token= query) is allowed — for on-stage/judge access from anywhere.
///   2. Loopback requests are allowed only in Development (local debugging).
///   3. Everything else is denied.
/// Set HANGFIRE_DASHBOARD_TOKEN in production to reach /jobs at all.
/// </summary>
public sealed class TokenDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public static readonly TokenDashboardAuthorizationFilter Instance = new();

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var cfg = http.RequestServices.GetRequiredService<IConfiguration>();
        var token = cfg["Hangfire:DashboardToken"] ?? cfg["HANGFIRE_DASHBOARD_TOKEN"];

        if (!string.IsNullOrEmpty(token))
        {
            var provided = !string.IsNullOrWhiteSpace(http.Request.Headers["X-Dashboard-Token"].ToString())
                ? http.Request.Headers["X-Dashboard-Token"].ToString()
                : http.Request.Query["dashboard_token"].ToString();

            if (ConstantTimeEquals(provided, token))
                return true;

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        // No token configured: loopback-only, and only in Development so an unconfigured
        // production deploy is locked out rather than accidentally open behind a proxy.
        var env = http.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        if (env.IsDevelopment() &&
            http.Connection.RemoteIpAddress is { } remote &&
            IPAddress.IsLoopback(remote))
            return true;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return false;
    }

    private static bool ConstantTimeEquals(string? a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return false;

        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
