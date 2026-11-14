using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Events;

public sealed class CaptionFinalizedHandler(
    ISessionRepository repo,
    IBackgroundJobScheduler? jobScheduler = null) : INotificationHandler<CaptionFinalized>
{
    public async Task Handle(CaptionFinalized notification, CancellationToken ct)
    {
        if (jobScheduler is not null)
        {
            jobScheduler.EnqueuePersistCaption(
                notification.SessionId,
                notification.OffsetMs,
                notification.Text,
                notification.SourceLanguage);
            return;
        }

        var session = await repo.GetWithTranscriptAsync(notification.SessionId, ct);
        if (session is null) return;

        session.Segments.Add(new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            SessionId = notification.SessionId,
            OffsetMs = notification.OffsetMs,
            Text = notification.Text,
            SourceLanguage = notification.SourceLanguage,
            CreatedAtUtc = DateTime.UtcNow
        });

        await repo.SaveChangesAsync(ct);
    }
}
