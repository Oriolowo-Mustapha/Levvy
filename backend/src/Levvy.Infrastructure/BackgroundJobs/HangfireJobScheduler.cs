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
}
