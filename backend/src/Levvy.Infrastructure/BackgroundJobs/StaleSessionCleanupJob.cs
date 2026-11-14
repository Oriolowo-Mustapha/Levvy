using Hangfire;
using Levvy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring Hangfire background job that closes abandoned live sessions older than 3 hours.
/// </summary>
public sealed class StaleSessionCleanupJob(
    LevvyDbContext db,
    ILogger<StaleSessionCleanupJob> logger)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddHours(-3);
        var staleSessions = await db.Sessions
            .Where(s => s.EndedAtUtc == null && s.StartedAtUtc < cutoff)
            .ToListAsync(ct);

        if (staleSessions.Count == 0)
            return;

        foreach (var s in staleSessions)
        {
            s.EndedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Closed {Count} stale live lecture sessions via Hangfire cleanup job.", staleSessions.Count);
    }
}
