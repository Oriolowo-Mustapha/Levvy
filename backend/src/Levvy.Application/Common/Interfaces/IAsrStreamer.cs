using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

/// <summary>
/// Speech-to-text boundary. Implemented by Groq (Whisper chunks) with Deepgram fallback.
/// </summary>
public interface IAsrStreamer
{
    Task<IReadOnlyList<TranscriptSegment>> TranscribeChunkAsync(
        Stream audioChunk,
        long chunkStartOffsetMs,
        string sourceLanguage,
        CancellationToken ct);
}
