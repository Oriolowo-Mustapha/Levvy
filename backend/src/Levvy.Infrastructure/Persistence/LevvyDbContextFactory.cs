using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Levvy.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can instantiate the Npgsql-only
/// LevvyDbContext without booting Levvy.Api. Resolves the connection string the same
/// way the app does: DATABASE_URL env var first, then a local dev default.
/// </summary>
public sealed class LevvyDbContextFactory : IDesignTimeDbContextFactory<LevvyDbContext>
{
    public LevvyDbContext CreateDbContext(string[] args)
    {
        var connString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Levvy")
            ?? "Host=localhost;Port=5432;Database=levvy;Username=levvy;Password=levvy";

        var options = new DbContextOptionsBuilder<LevvyDbContext>()
            .UseNpgsql(connString)
            .Options;

        return new LevvyDbContext(options);
    }
}
