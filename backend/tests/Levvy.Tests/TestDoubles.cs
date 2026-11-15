using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;

namespace Levvy.Tests;

/// <summary>Shared in-memory ISessionRepository fake for handler/unit tests (no EF required).</summary>
internal sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly List<Session> _sessions = new();

    public Task<Session?> GetByCodeAsync(string code, CancellationToken ct)
        => Task.FromResult(_sessions.FirstOrDefault(s => string.Equals(s.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<Session?> GetWithTranscriptAsync(Guid sessionId, CancellationToken ct)
        => Task.FromResult(_sessions.FirstOrDefault(s => s.Id == sessionId));

    public Task AddAsync(Session session, CancellationToken ct)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<bool> AddSegmentIfMissingAsync(TranscriptSegment segment, CancellationToken ct)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == segment.SessionId);
        if (session is null)
            return Task.FromResult(false);

        var duplicate = session.Segments.Any(s =>
            s.OffsetMs == segment.OffsetMs &&
            string.Equals(s.Text, segment.Text, StringComparison.Ordinal));
        if (duplicate)
            return Task.FromResult(false);

        session.Segments.Add(segment);
        return Task.FromResult(true);
    }

    public Task AddParticipantAsync(Participant participant, CancellationToken ct)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == participant.SessionId);
        session?.Participants.Add(participant);
        return Task.CompletedTask;
    }

    public Task AddQuestionAsync(Question question, CancellationToken ct)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == question.SessionId);
        session?.Questions.Add(question);
        return Task.CompletedTask;
    }
}

/// <summary>Shared IBackgroundJobScheduler spy that records enqueued jobs instead of running Hangfire.</summary>
internal sealed class SpyJobScheduler : IBackgroundJobScheduler
{
    public List<(Guid SessionId, long OffsetMs, string Text)> CaptionJobs { get; } = new();
    public List<Guid> StudyPackSessionIds { get; } = new();
    public List<Guid> DispatchBotSessionIds { get; } = new();
    public List<Guid> MonitorBotSessionIds { get; } = new();
    public List<int> MonitorBotAttempts { get; } = new();

    public string EnqueuePersistCaption(Guid sessionId, long offsetMs, string text, string sourceLanguage)
    {
        CaptionJobs.Add((sessionId, offsetMs, text));
        return Guid.NewGuid().ToString("N");
    }

    public string EnqueueGenerateStudyPack(Guid sessionId)
    {
        StudyPackSessionIds.Add(sessionId);
        return Guid.NewGuid().ToString("N");
    }

    public string EnqueueDispatchOnlineClassBot(Guid sessionId)
    {
        DispatchBotSessionIds.Add(sessionId);
        return Guid.NewGuid().ToString("N");
    }

    public string EnqueueMonitorOnlineClassBot(Guid sessionId, int attempt = 0)
    {
        MonitorBotSessionIds.Add(sessionId);
        MonitorBotAttempts.Add(attempt);
        return Guid.NewGuid().ToString("N");
    }
}
