namespace Levvy.Application.Common;

/// <summary>
/// Detects the supported online-class platform from a pasted meeting URL
/// (plan.md Phase 2: meet.google.com, zoom.us, teams.microsoft.com).
/// Host-aware: matches the URL authority (plus its subdomains), never raw substrings,
/// so lookalike hosts like evil.example.com/meet.google.com are rejected.
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

        if (!Uri.TryCreate(meetingUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            return false;

        var host = uri.Host.ToLowerInvariant();

        // Exact host or a subdomain of the real platform domain only.
        if (host == "meet.google.com" || host.EndsWith(".meet.google.com", StringComparison.Ordinal))
            platform = GoogleMeet;
        else if (host == "zoom.us" || host.EndsWith(".zoom.us", StringComparison.Ordinal))
            platform = Zoom;
        else if (host == "teams.microsoft.com" || host.EndsWith(".teams.microsoft.com", StringComparison.Ordinal) ||
                 host == "teams.live.com" || host.EndsWith(".teams.live.com", StringComparison.Ordinal))
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
