namespace Levvy.Domain.Entities;

/// <summary>
/// One finalized sentence of the lecture. OffsetMs anchors it into the recording
/// so every downstream artifact (translation, answer, flashcard) can cite it.
/// </summary>
public class TranscriptSegment
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public long OffsetMs { get; set; }                     // position in the recording
    public string Text { get; set; } = default!;
    public string SourceLanguage { get; set; } = "en";
    public DateTime CreatedAtUtc { get; set; }
}
