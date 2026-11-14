using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Levvy.Infrastructure.Persistence;

public class SessionRepository(LevvyDbContext db) : ISessionRepository
{
    public Task<Session?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.Sessions
            .Include(s => s.Participants)
            .Include(s => s.Segments)
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answer)
                    .ThenInclude(a => a!.Citations)
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Flashcards)
            .FirstOrDefaultAsync(s => s.Code == normalized, ct);
    }

    public Task<Session?> GetWithTranscriptAsync(Guid sessionId, CancellationToken ct)
    {
        return db.Sessions
            .Include(s => s.Participants)
            .Include(s => s.Segments)
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answer)
                    .ThenInclude(a => a!.Citations)
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Flashcards)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task AddAsync(Session session, CancellationToken ct)
    {
        await db.Sessions.AddAsync(session, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }
}
