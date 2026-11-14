using System.Net.WebSockets;

namespace Levvy.Api.Hubs;

public sealed class ConnectedClient(
    Guid connectionId,
    Guid participantId,
    string role,
    string preferredLanguage,
    WebSocket socket)
{
    public Guid ConnectionId { get; } = connectionId;
    public Guid ParticipantId { get; } = participantId;
    public string Role { get; } = role;
    public string PreferredLanguage { get; set; } = preferredLanguage;
    public WebSocket Socket { get; } = socket;
}
