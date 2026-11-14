using MediatR;

namespace Levvy.Application.Events;

/// <summary>Published on EndSession. Study pack generation hangs off this.</summary>
public sealed record SessionEnded(Guid SessionId) : INotification;
