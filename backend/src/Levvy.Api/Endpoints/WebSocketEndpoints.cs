using Levvy.Api.Hubs;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Levvy.Api.Endpoints;

public static class WebSocketEndpoints
{
    public static IEndpointRouteBuilder MapWebSocketEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.Map("/ws/{sessionId:guid}", async (
            HttpContext context,
            Guid sessionId,
            LectureSessionHub hub,
            ISessionRepository repo,
            IJwtTokenGenerator jwt,
            IUserRepository users,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest(new { error = "Expected a WebSocket upgrade request." });

            // Audit fix (C2): the client-declared role is only a request. Host privileges are
            // granted server-side via HostAccess; failed claimants are downgraded to viewer.
            var requestedRole = context.Request.Query["role"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "viewer";
            var effectiveRole = "viewer";

            if (requestedRole == "host")
            {
                var session = await repo.GetWithTranscriptAsync(sessionId, ct);
                if (session is null)
                    return Results.NotFound(new { error = "Session not found." });

                effectiveRole = await HostAccess.IsHostAuthorizedAsync(context, session, jwt, users, cfg, ct)
                    ? "host"
                    : "viewer";
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(context, sessionId, effectiveRole, socket, ct);
            return Results.Empty;
        });

        return routes;
    }
}
