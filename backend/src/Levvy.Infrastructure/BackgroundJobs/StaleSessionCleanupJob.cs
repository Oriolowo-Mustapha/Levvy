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
        // Audit fix (Minor): single bulk UPDATE in SQL instead of loading every stale session
        // into the change tracker just to stamp one column.
        var cutoff = DateTime.UtcNow.AddHours(-3);

        var updated = await db.Sessions
            .Where(s => s.EndedAtUtc == null && s.StartedAtUtc < cutoff)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.EndedAtUtc, DateTime.UtcNow), ct);

        if (updated == 0)
            return;

        logger.LogInformation("Closed {Count} stale live lecture sessions via Hangfire cleanup job.", updated);
    }
}
