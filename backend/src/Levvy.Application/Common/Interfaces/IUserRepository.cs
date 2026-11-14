using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

public sealed record UserProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string AuthProvider,
    string? AvatarUrl,
    string Role,
    string? Institution,
    string? Department,
    string PreferredLanguage,
    string AccessibilityMode,
    string CaptionFontSize,
    DateTime CreatedAtUtc,
    DateTime LastLoginAtUtc)
{
    public static UserProfileDto FromEntity(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.AuthProvider,
        user.AvatarUrl,
        user.Role,
        user.Institution,
        user.Department,
        user.PreferredLanguage,
        user.AccessibilityMode,
        user.CaptionFontSize,
        user.CreatedAtUtc,
        user.LastLoginAtUtc);
}

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserProfileDto User);

public sealed record DashboardSessionItemDto(
    Guid Id,
    string Code,
    string Title,
    bool IsLive,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int ParticipantCount,
    bool HasStudyPack,
    bool HasRecording);

public sealed record DashboardStudyPackItemDto(
    Guid StudyPackId,
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    string Summary,
    int FlashcardCount,
    DateTime GeneratedAtUtc);

public sealed record UserDashboardDto(
    UserProfileDto Profile,
    int TotalSessionsHosted,
    int TotalSessionsAttended,
    int TotalQuestionsAsked,
    int TotalStudyPacksAvailable,
    IReadOnlyList<DashboardSessionItemDto> HostedSessions,
    IReadOnlyList<DashboardSessionItemDto> AttendedSessions,
    IReadOnlyList<DashboardStudyPackItemDto> StudyPacks);

/// <summary>Persistence and dashboard projection boundary for User accounts.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByGoogleSubAsync(string googleSub, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<UserDashboardDto?> GetDashboardAsync(Guid userId, CancellationToken ct);
}
