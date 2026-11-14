using Hangfire;
using Levvy.Application.Common.Interfaces;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire implementation of IBackgroundJobScheduler for fire-and-forget background processing.
/// </summary>
public sealed class HangfireJobScheduler(IBackgroundJobClient jobClient) : IBackgroundJobScheduler
{
    public string EnqueuePersistCaption(Guid sessionId, long offsetMs, string text, string sourceLanguage)
    {
        return jobClient.Enqueue<CaptionPersistenceJob>(
            job => job.ExecuteAsync(sessionId, offsetMs, text, sourceLanguage, CancellationToken.None));
    }

    public string EnqueueGenerateStudyPack(Guid sessionId)
    {
        return jobClient.Enqueue<StudyPackGenerationJob>(
            job => job.ExecuteAsync(sessionId, CancellationToken.None));
    }

    public string EnqueueDispatchOnlineClassBot(Guid sessionId)
    {
        return jobClient.Enqueue<OnlineClassBotJob>(
            job => job.DispatchAsync(sessionId, CancellationToken.None));
    }

    public string EnqueueMonitorOnlineClassBot(Guid sessionId, int attempt = 0)
    {
        return jobClient.Schedule<OnlineClassBotJob>(
            job => job.MonitorAsync(sessionId, attempt, CancellationToken.None),
            TimeSpan.FromSeconds(30));
    }
}
