namespace Levvy.Domain.Entities;

/// <summary>
/// The grounded answer to a question. Status is honest by rule:
/// "answered" only when receipts point at real transcript segments,
/// "not_covered" when the lecture has not reached the topic yet.
/// </summary>
public class Answer
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = default!;
    public string Status { get; set; } = "answered";       // answered | not_covered
    public ICollection<AnswerCitation> Citations { get; set; } = new List<AnswerCitation>();
    public DateTime CreatedAtUtc { get; set; }
}
