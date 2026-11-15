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

    public async Task AddAsync(Session session, CancellationToken ct)
    {
        await db.Sessions.AddAsync(session, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
    }
}
