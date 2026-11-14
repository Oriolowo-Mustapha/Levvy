using MediatR;

namespace Levvy.Application.Commands;

/// <summary>
/// Ask Levvy, mid-lecture. Loads transcript so far, calls ILlmAnswerer,
/// persists Question + Answer with citations, and returns receipts for playback.
/// </summary>
public sealed record AskQuestionCommand(string SessionCode, Guid AskerId, string Text) : IRequest<AskQuestionResult>;
public sealed record AskQuestionResult(string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs);
