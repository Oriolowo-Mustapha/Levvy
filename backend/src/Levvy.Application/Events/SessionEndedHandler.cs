using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using MediatR;

namespace Levvy.Application.Events;

public sealed class SessionEndedHandler(
    ISender sender,
    IBackgroundJobScheduler? jobScheduler = null) : INotificationHandler<SessionEnded>
{
    public async Task Handle(SessionEnded notification, CancellationToken ct)
    {
        if (jobScheduler is not null)
        {
            jobScheduler.EnqueueGenerateStudyPack(notification.SessionId);
            return;
        }

        await sender.Send(new GenerateStudyPackCommand(notification.SessionId), ct);
    }
}
