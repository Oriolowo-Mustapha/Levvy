using Levvy.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Levvy.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core Npgsql migrations (used by dotnet ef CLI).
/// Resolves the connection string from backend/.env or DATABASE_URL, falling back
/// to the same default the app uses, so no running API instance is required.
/// </summary>
public sealed class LevvyDbContextFactory : IDesignTimeDbContextFactory<LevvyDbContext>
{
    public LevvyDbContext CreateDbContext(string[] args)
    {
        var env = DotEnvLoader.LoadFromDefaultLocations();
        var connString =
            (env.TryGetValue("DATABASE_URL", out var url) && !string.IsNullOrWhiteSpace(url) ? url : null)
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? DependencyInjection.DefaultConnectionString;

        return new LevvyDbContext(new DbContextOptionsBuilder<LevvyDbContext>()
            .UseNpgsql(connString)
            .Options);
    }
}
