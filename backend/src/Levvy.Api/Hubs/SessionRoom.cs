using System.Collections.Concurrent;
using Levvy.Domain.Entities;

namespace Levvy.Api.Hubs;

public sealed class SessionRoom(Guid sessionId, string sessionCode)
{
    private readonly object _lock = new();
    private readonly List<TranscriptSegment> _segments = new();
    private long _nextChunkOffsetMs;

    public Guid SessionId { get; } = sessionId;
    public string SessionCode { get; } = sessionCode;
    public string SourceLanguage { get; set; } = "en";
    public ConcurrentDictionary<Guid, ConnectedClient> Clients { get; } = new();

    public long ReserveNextChunkOffsetMs(long defaultChunkDurationMs)
    {
        lock (_lock)
        {
            var current = _nextChunkOffsetMs;
            _nextChunkOffsetMs += defaultChunkDurationMs;
            return current;
        }
    }

    public void SetNextChunkOffsetMs(long offsetMs)
    {
        lock (_lock)
        {
            _nextChunkOffsetMs = offsetMs;
        }
    }

    public void AppendSegment(TranscriptSegment segment)
    {
        lock (_lock)
        {
            _segments.Add(segment);
            if (segment.OffsetMs >= _nextChunkOffsetMs)
            {
                _nextChunkOffsetMs = segment.OffsetMs + 1000;
            }
        }
    }

    public IReadOnlyList<TranscriptSegment> GetTranscriptSnapshot()
    {
        lock (_lock)
        {
            return _segments.OrderBy(s => s.OffsetMs).ToList();
        }
    }
}
