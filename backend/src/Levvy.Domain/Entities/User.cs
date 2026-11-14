namespace Levvy.Domain.Entities;

/// <summary>
/// Registered Levvy user (student or instructor) supporting email/password and Google Sign-In.
/// Stores personal accessibility defaults and links to hosted/attended sessions.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PasswordHash { get; set; }
    public string AuthProvider { get; set; } = "local"; // "local" | "google"
    public string? GoogleSub { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "student"; // "student" | "instructor"
    public string? Institution { get; set; }
    public string? Department { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string AccessibilityMode { get; set; } = "standard"; // "standard" | "deaf_hoh" | "international"
    public string CaptionFontSize { get; set; } = "md"; // "sm" | "md" | "lg" | "xl"
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginAtUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
