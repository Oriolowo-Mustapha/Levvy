using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Levvy.Infrastructure.Authentication;

/// <summary>Short-lived, single-use credentials scoped to one host session. Never stores bearer tokens.</summary>
public sealed class WebSocketTicketStore(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (Guid SessionId, DateTimeOffset Expires)> _tickets = new();

    public string Issue(Guid sessionId)
    {
        var now = clock.GetUtcNow();
        foreach (var entry in _tickets)
            if (entry.Value.Expires <= now) _tickets.TryRemove(entry.Key, out _);
        if (_tickets.Count >= 10_000) throw new InvalidOperationException("Too many pending host connections.");
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tickets[ticket] = (sessionId, now.AddSeconds(30));
        return ticket;
    }

    public bool Consume(string ticket, Guid sessionId) =>
        !string.IsNullOrEmpty(ticket) &&
        _tickets.TryRemove(ticket, out var entry) &&
        entry.SessionId == sessionId && entry.Expires > clock.GetUtcNow();
}
