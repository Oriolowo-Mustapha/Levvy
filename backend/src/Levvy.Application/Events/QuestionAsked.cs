using MediatR;

namespace Levvy.Application.Events;

/// <summary>Published when a student asks a question. Grounded answering hangs off this.</summary>
public sealed record QuestionAsked(Guid SessionId, Guid QuestionId, string Text) : INotification;
