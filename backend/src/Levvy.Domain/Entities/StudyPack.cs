namespace Levvy.Domain.Entities;

/// <summary>
/// The after-class pack generated when a session ends.
/// Every item must carry its source segment's offset (cite-or-say-not-covered).
/// </summary>
public class StudyPack
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = default!;
    public ICollection<Flashcard> Flashcards { get; set; } = new List<Flashcard>();
    public DateTime GeneratedAtUtc { get; set; }
}
