namespace Levvy.Application.Common;

/// <summary>
/// Detects the supported online-class platform from a pasted meeting URL
/// (plan.md Phase 2: meet.google.com, zoom.us, teams.microsoft.com).
/// </summary>
public static class MeetingUrlParser
{
    public const string GoogleMeet = "google_meet";
    public const string Zoom = "zoom";
    public const string Teams = "teams";

    public static bool TryGetPlatform(string? meetingUrl, out string platform)
    {
        platform = "";
        if (string.IsNullOrWhiteSpace(meetingUrl))
            return false;

        var url = meetingUrl.Trim().ToLowerInvariant();

        if (url.Contains("meet.google.com"))
            platform = GoogleMeet;
        else if (url.Contains("zoom.us"))
            platform = Zoom;
        else if (url.Contains("teams.microsoft.com") || url.Contains("teams.live.com"))
            platform = Teams;
        else
            return false;

        return true;
    }

    /// <summary>Throws <see cref="ArgumentException"/> for unsupported URLs (handler-side use).</summary>
    public static string GetPlatform(string meetingUrl)
        => TryGetPlatform(meetingUrl, out var platform)
            ? platform
            : throw new ArgumentException(
                $"Unsupported meeting URL. Expected a meet.google.com, zoom.us or teams.microsoft.com link but got: '{meetingUrl}'.",
                nameof(meetingUrl));
}
