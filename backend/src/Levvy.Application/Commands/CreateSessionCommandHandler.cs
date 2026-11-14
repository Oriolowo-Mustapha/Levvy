using Levvy.Application.Common;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class CreateSessionCommandHandler(ISessionRepository repo)
    : IRequestHandler<CreateSessionCommand, CreateSessionResult>
{
    public async Task<CreateSessionResult> Handle(CreateSessionCommand request, CancellationToken ct)
    {
        var code = SessionCodeGenerator.Generate();
        var hostId = request.HostId == Guid.Empty ? Guid.NewGuid() : request.HostId;

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = request.Title.Trim(),
            HostId = hostId,
            StartedAtUtc = DateTime.UtcNow
        };

        await repo.AddAsync(session, ct);
        await repo.SaveChangesAsync(ct);

        return new CreateSessionResult(session.Id, session.Code);
    }
}
