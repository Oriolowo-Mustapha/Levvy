using MediatR;

namespace Levvy.Application.Queries;

public sealed record GetStudyPackByCodeQuery(string Code) : IRequest<StudyPackDto?>;
public sealed record StudyPackFlashcardDto(Guid Id, string Front, string Back, long SourceOffsetMs);
public sealed record StudyPackPartDto(Guid Id, string Title, long StartOffsetMs, int Ordinal);
public sealed record StudyPackDto(
    Guid SessionId,
    string Code,
    string Title,
    string Summary,
    IReadOnlyList<StudyPackFlashcardDto> Flashcards,
    IReadOnlyList<TranscriptLine> Transcript,
    IReadOnlyList<StudyPackPartDto> Parts);
