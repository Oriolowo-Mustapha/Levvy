using Levvy.Application.Common.Interfaces;
using MediatR;

namespace Levvy.Application.Queries;

public sealed class GetTranscriptQueryHandler(ISessionRepository repo)
    : IRequestHandler<GetTranscriptQuery, IReadOnlyList<TranscriptLine>>
{
    public async Task<IReadOnlyList<TranscriptLine>> Handle(GetTranscriptQuery request, CancellationToken ct)
    {
        var session = await repo.GetWithTranscriptAsync(request.SessionId, ct);
        if (session is null) return Array.Empty<TranscriptLine>();

        var ordered = session.Segments.OrderBy(s => s.OffsetMs).ToList();
        return ordered
            .Select((s, idx) => new TranscriptLine(s.OffsetMs, s.Text, idx == ordered.Count - 1))
            .ToList();
    }
}
