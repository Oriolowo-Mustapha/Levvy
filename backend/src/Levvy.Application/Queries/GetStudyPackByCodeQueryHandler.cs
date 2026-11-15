using Levvy.Application.Common.Interfaces;
using MediatR;

namespace Levvy.Application.Queries;

public sealed class GetStudyPackByCodeQueryHandler(ISessionRepository repo)
    : IRequestHandler<GetStudyPackByCodeQuery, StudyPackDto?>
{
    public async Task<StudyPackDto?> Handle(GetStudyPackByCodeQuery request, CancellationToken ct)
    {
        var session = await repo.GetByCodeAsync(request.Code, ct);
        if (session is null) return null;

        var ordered = session.Segments.OrderBy(s => s.OffsetMs).ToList();
        var transcript = ordered
            .Select((s, idx) => new TranscriptLine(s.OffsetMs, s.Text, idx == ordered.Count - 1))
            .ToList();

        var summary = session.StudyPack?.Summary ?? "Study pack is being generated from the lecture transcript.";
        var cards = session.StudyPack?.Flashcards
            .OrderBy(f => f.SourceOffsetMs)
            .Select(f => new StudyPackFlashcardDto(f.Id, f.Front, f.Back, f.SourceOffsetMs))
            .ToList() ?? new List<StudyPackFlashcardDto>();
        var parts = session.StudyPack?.Parts
            .OrderBy(p => p.Ordinal)
            .Select(p => new StudyPackPartDto(p.Id, p.Title, p.StartOffsetMs, p.Ordinal))
            .ToList() ?? new List<StudyPackPartDto>();

        return new StudyPackDto(session.Id, session.Code, session.Title, summary, cards, transcript, parts);
    }
}
