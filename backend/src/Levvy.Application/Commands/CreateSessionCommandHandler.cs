using System.Security.Cryptography;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class CreateSessionCommandHandler(ISessionRepository repo)
    : IRequestHandler<CreateSessionCommand, CreateSessionResult>
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<CreateSessionResult> Handle(CreateSessionCommand request, CancellationToken ct)
    {
        var code = GenerateShortCode();
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

    private static string GenerateShortCode()
    {
        Span<char> chars = stackalloc char[7];
        for (var i = 0; i < 7; i++)
        {
            if (i == 3)
            {
                chars[i] = '-';
                continue;
            }
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
