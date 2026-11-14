namespace Levvy.Domain.Entities;

/// <summary>
/// A receipt: the timestamp in the recording that backs (part of) an answer.
/// </summary>
public class AnswerCitation
{
    public Guid Id { get; set; }
    public Guid AnswerId { get; set; }
    public long OffsetMs { get; set; }
    public string SegmentText { get; set; } = default!;
}
