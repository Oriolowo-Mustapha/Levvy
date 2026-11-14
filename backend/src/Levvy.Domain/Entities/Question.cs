namespace Levvy.Domain.Entities;

/// <summary>
/// A student's mid-lecture question. Private to the asker by default.
/// </summary>
public class Question
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid AskerId { get; set; }
    public string Text { get; set; } = default!;
    public DateTime AskedAtUtc { get; set; }
    public Answer? Answer { get; set; }
}
