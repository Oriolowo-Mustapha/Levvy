using Levvy.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Levvy.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core Npgsql migrations (used by dotnet ef CLI).
/// </summary>
public class LevvyDbContextFactory : IDesignTimeDbContextFactory<LevvyDbContext>
{
    public LevvyDbContext CreateDbContext(string[] args)
    {
        var env = DotEnvLoader.LoadFromDefaultLocations();
        var connString =
            (env.TryGetValue("ConnectionStrings:Levvy", out var cs) && !string.IsNullOrWhiteSpace(cs) ? cs : null)
            ?? (env.TryGetValue("DATABASE_URL", out var url) && !string.IsNullOrWhiteSpace(url) ? url : null)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Levvy")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? DependencyInjection.DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<LevvyDbContext>();
        optionsBuilder.UseNpgsql(connString);

        return new LevvyDbContext(optionsBuilder.Options);
    }
}
