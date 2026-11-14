using MediatR;

namespace Levvy.Application.Events;

/// <summary>Published when ASR finalizes a chunk segment. Translation + persistence hang off this.</summary>
public sealed record CaptionFinalized(Guid SessionId, long OffsetMs, string Text, string SourceLanguage) : INotification;
