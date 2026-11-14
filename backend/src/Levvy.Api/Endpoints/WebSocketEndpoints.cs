using Levvy.Api.Hubs;

namespace Levvy.Api.Endpoints;

public static class WebSocketEndpoints
{
    public static IEndpointRouteBuilder MapWebSocketEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.Map("/ws/{sessionId:guid}", async (
            HttpContext context,
            Guid sessionId,
            LectureSessionHub hub,
            CancellationToken ct) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest(new { error = "Expected a WebSocket upgrade request." });

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await hub.HandleConnectionAsync(context, sessionId, socket, ct);
            return Results.Empty;
        });

        return routes;
    }
}
