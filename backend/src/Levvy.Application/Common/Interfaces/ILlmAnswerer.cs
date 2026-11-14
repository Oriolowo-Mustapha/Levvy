using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Grounded Q&amp;A and study-pack generation. Implementations MUST obey cite-or-say-not-covered:
/// return citations into the transcript, or status not_covered. Never general knowledge.
/// </summary>
public interface ILlmAnswerer
{
    Task<(string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs)> AnswerFromTranscriptAsync(
        string question,
        IReadOnlyList<TranscriptSegment> transcriptSoFar,
        CancellationToken ct);

    Task<GeneratedStudyPackResult> GenerateStudyPackAsync(
        IReadOnlyList<TranscriptSegment> transcript,
        CancellationToken ct);
}

public sealed record GeneratedFlashcardItem(string Front, string Back, long SourceOffsetMs);
public sealed record GeneratedStudyPackResult(string Summary, IReadOnlyList<GeneratedFlashcardItem> Flashcards);
