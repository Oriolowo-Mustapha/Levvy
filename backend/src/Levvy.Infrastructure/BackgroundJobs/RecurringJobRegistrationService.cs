using Hangfire;
using Microsoft.Extensions.Hosting;

namespace Levvy.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted service that registers recurring Hangfire jobs once the server starts.
/// </summary>
public sealed class RecurringJobRegistrationService(IRecurringJobManager recurringJobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<StaleSessionCleanupJob>(
            "stale-session-cleanup",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Hourly);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
