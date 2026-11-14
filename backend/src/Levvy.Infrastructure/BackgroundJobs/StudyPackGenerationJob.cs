using Hangfire;
using Levvy.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire background job that generates the grounded after-class Study Pack (Summary + Flashcards)
/// with automatic retry resilience when a lecture session ends.
/// </summary>
public sealed class StudyPackGenerationJob(
    ISender sender,
    ILogger<StudyPackGenerationJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        logger.LogInformation("Hangfire starting StudyPack generation for session {SessionId}", sessionId);
        await sender.Send(new GenerateStudyPackCommand(sessionId), ct);
    }
}
