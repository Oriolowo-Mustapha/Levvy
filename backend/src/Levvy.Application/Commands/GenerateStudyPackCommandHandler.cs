using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class GenerateStudyPackCommandHandler(ISessionRepository repo, ILlmAnswerer answerer)
    : IRequestHandler<GenerateStudyPackCommand, Guid>
{
    public async Task<Guid> Handle(GenerateStudyPackCommand request, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(request.SessionId, ct)
            ?? throw new KeyNotFoundException($"Session '{request.SessionId}' was not found.");

        if (session.StudyPack is not null)
            return session.StudyPack.Id;

        var orderedSegments = session.Segments.OrderBy(s => s.OffsetMs).ToList();
        var generated = await answerer.GenerateStudyPackAsync(orderedSegments, ct);

        var packId = Guid.NewGuid();
        var pack = new StudyPack
        {
            Id = packId,
            SessionId = session.Id,
            Summary = generated.Summary,
            GeneratedAtUtc = DateTime.UtcNow,
            Flashcards = generated.Flashcards.Select(f => new Flashcard
            {
                Id = Guid.NewGuid(),
                StudyPackId = packId,
                Front = f.Front,
                Back = f.Back,
                SourceOffsetMs = f.SourceOffsetMs
            }).ToList(),
            Parts = generated.Parts.Select((p, idx) => new StudyPackPart
            {
                Id = Guid.NewGuid(),
                StudyPackId = packId,
                Title = p.Title,
                StartOffsetMs = p.StartOffsetMs,
                Ordinal = idx + 1
            }).ToList()
        };

        await repo.AddStudyPackAsync(pack, ct);

        return pack.Id;
    }
}
