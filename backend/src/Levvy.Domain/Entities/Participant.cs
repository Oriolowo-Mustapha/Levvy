namespace Levvy.Domain.Entities;

/// <summary>
/// Someone in the room: host or viewer. PreferredLanguage drives their subtitle track.
/// </summary>
public class Participant
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? UserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public bool IsHost { get; set; }
    public string PreferredLanguage { get; set; } = "en";  // BCP-47: en, es, hi, ta, ...
    public DateTime JoinedAtUtc { get; set; }
}
