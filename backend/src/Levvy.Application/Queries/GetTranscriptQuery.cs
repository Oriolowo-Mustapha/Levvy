using MediatR;

namespace Levvy.Application.Queries;

public sealed record GetTranscriptQuery(Guid SessionId) : IRequest<IReadOnlyList<TranscriptLine>>;
public sealed record TranscriptLine(long OffsetMs, string Text, bool IsCurrent);
