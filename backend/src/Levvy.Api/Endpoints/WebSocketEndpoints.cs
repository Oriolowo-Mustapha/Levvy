using Levvy.Api.Hubs;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Levvy.Infrastructure.Authentication;

namespace Levvy.Api.Endpoints;

public static class WebSocketEndpoints
{
    public static IEndpointRouteBuilder MapWebSocketEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/sessions/{sessionId:guid}/ws-ticket", async (
            HttpContext context, Guid sessionId, ISessionRepository repo,
            IJwtTokenGenerator jwt, WebSocketTicketStore tickets, CancellationToken ct) =>
        {
            var userId = HttpUserResolver.TryResolveFromBearer(context, jwt);
            if (userId is null) return Results.Unauthorized();
            var session = await repo.GetWithTranscriptAsync(sessionId, ct);
            if (session is null) return Results.NotFound();
            if (session.HostId != userId) return Results.StatusCode(403);
            if (session.EndedAtUtc is not null) return Results.Conflict(new { error = "This lecture has ended." });
            return Results.Ok(new { ticket = tickets.Issue(sessionId), expiresInSeconds = 30 });
        });

        routes.Map("/ws/{sessionId:guid}", async (
            HttpContext context,
            Guid sessionId,
            LectureSessionHub hub,
            ISessionRepository repo,
            IJwtTokenGenerator jwt,
            IUserRepository users,
            IConfiguration cfg,
            WebSocketTicketStore tickets,
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

                var ticket = context.Request.Query["ticket"].ToString();
                var authorized = !string.IsNullOrEmpty(ticket)
                    ? tickets.Consume(ticket, sessionId)
                    : await HostAccess.IsHostAuthorizedAsync(context, session, jwt, users, cfg, ct);
                if (!authorized) return Results.StatusCode(403);
                if (session.EndedAtUtc is not null) return Results.Conflict();
                effectiveRole = "host";
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(context, sessionId, effectiveRole, socket, ct);
            return Results.Empty;
        });

        return routes;
    }
}
