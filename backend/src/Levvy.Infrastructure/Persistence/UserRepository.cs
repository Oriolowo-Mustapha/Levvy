using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Levvy.Infrastructure.Persistence;

public sealed class UserRepository(LevvyDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public Task<User?> GetByGoogleSubAsync(string googleSub, CancellationToken ct)
    {
        var normalized = googleSub.Trim();
        return db.Users.FirstOrDefaultAsync(u => u.GoogleSub == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
        => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);

    public async Task<UserDashboardDto?> GetDashboardAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var hostedRaw = await db.Sessions
            .AsNoTracking()
            .Where(s => s.HostId == userId)
            .Include(s => s.Participants)
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Flashcards)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync(ct);

        var attendedRaw = await db.Sessions
            .AsNoTracking()
            .Where(s => s.HostId != userId && s.Participants.Any(p => p.UserId == userId))
            .Include(s => s.Participants)
            .Include(s => s.StudyPack)
                .ThenInclude(p => p!.Flashcards)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync(ct);

        var questionsAsked = await db.Questions
            .AsNoTracking()
            .CountAsync(q => q.AskerId == userId, ct);

        var hostedItems = hostedRaw.Select(MapSessionItem).ToList();
        var attendedItems = attendedRaw.Select(MapSessionItem).ToList();

        var studyPacks = hostedRaw
            .Concat(attendedRaw)
            .Where(s => s.StudyPack is not null)
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .OrderByDescending(s => s.StudyPack!.GeneratedAtUtc)
            .Select(s => new DashboardStudyPackItemDto(
                s.StudyPack!.Id,
                s.Id,
                s.Code,
                s.Title,
                s.StudyPack.Summary,
                s.StudyPack.Flashcards.Count,
                s.StudyPack.GeneratedAtUtc))
            .ToList();

        return new UserDashboardDto(
            UserProfileDto.FromEntity(user),
            hostedItems.Count,
            attendedItems.Count,
            questionsAsked,
            studyPacks.Count,
            hostedItems,
            attendedItems,
            studyPacks);
    }

    private static DashboardSessionItemDto MapSessionItem(Session s) => new(
        s.Id,
        s.Code,
        s.Title,
        s.EndedAtUtc is null,
        s.StartedAtUtc,
        s.EndedAtUtc,
        s.Participants.Count,
        s.StudyPack is not null,
        !string.IsNullOrWhiteSpace(s.RecordingKey));
}
