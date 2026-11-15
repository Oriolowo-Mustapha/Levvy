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
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Parts)
            .AsSplitQuery() // Audit fix (M5): avoid cartesian blow-up on the multi-include fan-out.
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
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Parts)
            .AsSplitQuery() // Audit fix (M5)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<bool> AddSegmentIfMissingAsync(TranscriptSegment segment, CancellationToken ct)
    {
        // Audit fix (H6): append-only write without materializing the whole session graph,
        // and idempotent against Hangfire retries by (SessionId, OffsetMs, Text).
        var exists = await db.TranscriptSegments
            .AnyAsync(s =>
                s.SessionId == segment.SessionId &&
                s.OffsetMs == segment.OffsetMs &&
                s.Text == segment.Text, ct);

        if (exists)
            return false;

        db.TranscriptSegments.Add(segment);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task AddParticipantAsync(Participant participant, CancellationToken ct)
    {
        db.Participants.Add(participant);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddQuestionAsync(Question question, CancellationToken ct)
    {
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddStudyPackAsync(StudyPack pack, CancellationToken ct)
    {
        var existingId = await db.StudyPacks
            .Where(p => p.SessionId == pack.SessionId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId.HasValue)
        {
            pack.Id = existingId.Value;
            return;
        }

        // Explicitly add the aggregate so EF Core tracks pre-assigned Guids as Added, not Modified.
        db.StudyPacks.Add(pack);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(pack).State = EntityState.Detached;
            foreach (var card in pack.Flashcards)
                db.Entry(card).State = EntityState.Detached;
            foreach (var part in pack.Parts)
                db.Entry(part).State = EntityState.Detached;

            var concurrentId = await db.StudyPacks
                .Where(p => p.SessionId == pack.SessionId)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (concurrentId.HasValue)
            {
                pack.Id = concurrentId.Value;
                return;
            }

            throw;
        }
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
