namespace Levvy.Domain.Entities;

/// <summary>
/// A topic-level part ("chapter") of a lecture, anchored to the transcript
/// segment offset where it begins. Generated with the study pack, grounded the
/// same way: StartOffsetMs must match a real segment offset.
/// </summary>
public class StudyPackPart
{
    public Guid Id { get; set; }
    public Guid StudyPackId { get; set; }
    public string Title { get; set; } = default!;
    public long StartOffsetMs { get; set; }
    public int Ordinal { get; set; }
}
