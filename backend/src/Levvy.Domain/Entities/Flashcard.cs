namespace Levvy.Domain.Entities;

/// <summary>
/// A study flashcard anchored to a specific recording offset (SourceOffsetMs).
/// </summary>
public class Flashcard
{
    public Guid Id { get; set; }
    public Guid StudyPackId { get; set; }
    public string Front { get; set; } = default!;
    public string Back { get; set; } = default!;
    public long SourceOffsetMs { get; set; }
}
