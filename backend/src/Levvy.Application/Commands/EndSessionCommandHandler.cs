using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class EndSessionCommandHandler(ISessionRepository repo, IPublisher publisher)
    : IRequestHandler<EndSessionCommand>
{
    public async Task Handle(EndSessionCommand request, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(request.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");

        if (session.EndedAtUtc is null)
        {
            session.EndedAtUtc = DateTime.UtcNow;
            await repo.SaveChangesAsync(ct);
        }

        await publisher.Publish(new SessionEnded(session.Id), ct);
    }
}
