using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

/// <summary>Session persistence boundary (EF Core in Infrastructure).</summary>
public interface ISessionRepository
{
    Task<Session?> GetByCodeAsync(string code, CancellationToken ct);
    Task<Session?> GetWithTranscriptAsync(Guid sessionId, CancellationToken ct);
    Task AddAsync(Session session, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Appends a transcript segment without loading the session graph; returns false when a
    /// segment with the same (SessionId, OffsetMs, Text) already exists (idempotent retries).
    /// </summary>
    Task<bool> AddSegmentIfMissingAsync(TranscriptSegment segment, CancellationToken ct);
    Task AddParticipantAsync(Participant participant, CancellationToken ct);
    Task AddQuestionAsync(Question question, CancellationToken ct);
    Task AddStudyPackAsync(StudyPack pack, CancellationToken ct);
}
