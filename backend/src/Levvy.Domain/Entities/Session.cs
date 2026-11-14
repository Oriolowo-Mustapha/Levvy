namespace Levvy.Domain.Entities;

/// <summary>
/// A live lecture session. Created by a host, joined by students via a short code.
/// </summary>
public class Session
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;          // e.g. K4F-8Q2
    public string Title { get; set; } = default!;         // e.g. "Elasticity of demand - Lecture 7"
    public Guid HostId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string? RecordingKey { get; set; }             // object storage key of the audio fixture

    // Online class bot (plan.md Phase 2 — Google Meet & Online Class Integration)
    public string? MeetingPlatform { get; set; }          // google_meet | zoom | teams
    public string? ExternalMeetingUrl { get; set; }       // e.g. https://meet.google.com/xxx-yyyy-zzz
    public string? BotStatus { get; set; }                // pending | joining | active | ended | failed | unconfigured
    public string? ExternalBotId { get; set; }            // bot id at the external provider (Recall.ai / MeetingBaas)

    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
    public ICollection<TranscriptSegment> Segments { get; set; } = new List<TranscriptSegment>();
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public StudyPack? StudyPack { get; set; }
}
