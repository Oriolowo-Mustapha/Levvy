namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Abstraction over Hangfire background job scheduling so Application handlers remain decoupled from Infrastructure.
/// </summary>
public interface IBackgroundJobScheduler
{
    string EnqueuePersistCaption(Guid sessionId, long offsetMs, string text, string sourceLanguage);
    string EnqueueGenerateStudyPack(Guid sessionId);
    string EnqueueDispatchOnlineClassBot(Guid sessionId);

    /// <param name="sessionId">Session whose bot is being polled.</param>
    /// <param name="attempt">Zero-based poll count; jobs stop re-chaining past MaxMonitorAttempts (~20 min at 30s spacing).</param>
    string EnqueueMonitorOnlineClassBot(Guid sessionId, int attempt = 0);
}
