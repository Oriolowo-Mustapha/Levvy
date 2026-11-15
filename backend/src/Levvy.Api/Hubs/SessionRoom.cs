using System.Collections.Concurrent;
using Levvy.Domain.Entities;

namespace Levvy.Api.Hubs;

public sealed class SessionRoom(Guid sessionId, string sessionCode)
{
    // A caption whose sentence is still open is held until the next chunk
    // completes it, so the transcript reads in sentences instead of 5s chunks.
    // The next chunk always flushes it (joined, or emitted as-is), so a held
    // caption never waits longer than one chunk (~5s) plus session end.
    private const int MaxRunawayTailLength = 300;
    private const int MaxMergedLength = 600;

    private readonly object _lock = new();
    private readonly List<TranscriptSegment> _segments = new();
    private long _nextChunkOffsetMs;
    private TranscriptSegment? _pendingTail;

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

    /// <summary>
    /// Joins caption fragments into sentence-complete segments before broadcast.
    /// The open tail of the previous chunk is prepended to the first segment of
    /// the next chunk (same speaker only); the new open tail is held back until
    /// the next chunk or session end. Call even with an empty chunk to flush.
    /// </summary>
    public IReadOnlyList<TranscriptSegment> PrepareForBroadcast(
        IReadOnlyList<TranscriptSegment> incoming)
    {
        lock (_lock)
        {
            var working = incoming.OrderBy(s => s.OffsetMs).ToList();
            var ready = new List<TranscriptSegment>();

            if (_pendingTail is not null)
            {
                if (working.Count > 0 && CanJoin(_pendingTail, working[0]))
                {
                    working[0] = Join(_pendingTail, working[0]);
                    _pendingTail = null;
                }
                else
                {
                    // Nothing to join onto (silence, speaker change, end of
                    // lecture): flush the tail as its own segment.
                    ready.Add(_pendingTail);
                    _pendingTail = null;
                }
            }

            for (var i = 0; i < working.Count; i++)
            {
                var isOpenTail =
                    i == working.Count - 1 &&
                    !LooksComplete(working[i].Text);
                if (isOpenTail)
                {
                    _pendingTail = working[i];
                }
                else
                {
                    ready.Add(working[i]);
                }
            }

            return ready;
        }
    }

    /// <summary>Flushes any held tail regardless of age (used when a session ends).</summary>
    public TranscriptSegment? TakePendingTail()
    {
        lock (_lock)
        {
            var tail = _pendingTail;
            _pendingTail = null;
            return tail;
        }
    }

    private static bool CanJoin(TranscriptSegment tail, TranscriptSegment next)
    {
        if (tail.Text.Length + next.Text.Length + 1 > MaxMergedLength)
            return false;

        var tailSpeaker = SpeakerLabel(tail.Text);
        var nextSpeaker = SpeakerLabel(next.Text);
        return string.Equals(tailSpeaker, nextSpeaker, StringComparison.Ordinal);
    }

    private static TranscriptSegment Join(TranscriptSegment tail, TranscriptSegment next)
    {
        return new TranscriptSegment
        {
            Id = tail.Id,
            SessionId = tail.SessionId,
            OffsetMs = tail.OffsetMs,
            Text = $"{tail.Text} {next.Text}",
            SourceLanguage = tail.SourceLanguage,
            CreatedAtUtc = tail.CreatedAtUtc
        };
    }

    private static string? SpeakerLabel(string text)
    {
        // Deepgram diarization prefixes utterances with "[Speaker 2] ".
        var end = text.IndexOf(']');
        return end > 1 && text.StartsWith("[Speaker", StringComparison.OrdinalIgnoreCase)
            ? text[..(end + 1)]
            : null;
    }

    private static bool LooksComplete(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (text.Length >= MaxRunawayTailLength)
            return true; // some languages lack terminal punctuation; don't hold forever
        var last = text.TrimEnd()[^1];
        return last is '.' or '!' or '?' or '…' or '。' or '！' or '？';
    }
}
