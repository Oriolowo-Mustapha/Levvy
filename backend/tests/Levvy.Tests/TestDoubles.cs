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
}

/// <summary>Shared IBackgroundJobScheduler spy that records enqueued jobs instead of running Hangfire.</summary>
internal sealed class SpyJobScheduler : IBackgroundJobScheduler
{
    public List<(Guid SessionId, long OffsetMs, string Text)> CaptionJobs { get; } = new();
    public List<Guid> StudyPackSessionIds { get; } = new();
    public List<Guid> DispatchBotSessionIds { get; } = new();
    public List<Guid> MonitorBotSessionIds { get; } = new();

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

    public string EnqueueMonitorOnlineClassBot(Guid sessionId)
    {
        MonitorBotSessionIds.Add(sessionId);
        return Guid.NewGuid().ToString("N");
    }
}
