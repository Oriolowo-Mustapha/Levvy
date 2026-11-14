namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Abstraction over Hangfire background job scheduling so Application handlers remain decoupled from Infrastructure.
/// </summary>
public interface IBackgroundJobScheduler
{
    string EnqueuePersistCaption(Guid sessionId, long offsetMs, string text, string sourceLanguage);
    string EnqueueGenerateStudyPack(Guid sessionId);
    string EnqueueDispatchOnlineClassBot(Guid sessionId);
    string EnqueueMonitorOnlineClassBot(Guid sessionId);
}
