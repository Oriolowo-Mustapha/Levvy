using Levvy.Infrastructure.Authentication;

namespace Levvy.Tests;

public class WebSocketTicketTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void TicketIsSingleUse()
    {
        var store = new WebSocketTicketStore(TimeProvider.System);
        var session = Guid.NewGuid();
        var ticket = store.Issue(session);
        Assert.True(store.Consume(ticket, session));
        Assert.False(store.Consume(ticket, session));
    }

    [Fact]
    public void TicketCannotAuthorizeAnotherSession()
    {
        var store = new WebSocketTicketStore(TimeProvider.System);
        Assert.False(store.Consume(store.Issue(Guid.NewGuid()), Guid.NewGuid()));
        Assert.False(store.Consume("invalid", Guid.NewGuid()));
    }

    [Fact]
    public void TicketExpiresAtThirtySeconds()
    {
        var clock = new Clock();
        var store = new WebSocketTicketStore(clock);
        var session = Guid.NewGuid();
        var ticket = store.Issue(session);
        clock.Now = clock.Now.AddSeconds(30);
        Assert.False(store.Consume(ticket, session));
    }
}
