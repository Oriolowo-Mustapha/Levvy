using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Levvy.Infrastructure.Persistence;

/// <summary>
/// Initializes the database schema on startup: uses EF Core migrations for PostgreSQL
/// (safely baselining tables created previously by EnsureCreated) and EnsureCreated for local SQLite.
/// </summary>
public static class DatabaseInitializer
{
    private const string InitialMigrationId = "20261114031741_InitialCreate";
    private const string EfProductVersion = "10.0.4";

    public static void Initialize(LevvyDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!db.Database.IsNpgsql())
        {
            db.Database.EnsureCreated();
            return;
        }

        BaselineExistingEnsureCreatedSchemaIfNeeded(db);
        db.Database.Migrate();
    }

    private static void BaselineExistingEnsureCreatedSchemaIfNeeded(LevvyDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            conn.Open();

        try
        {
            using var checkTablesCmd = conn.CreateCommand();
            checkTablesCmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'Sessions'
                );
                """;
            var sessionsExists = Convert.ToBoolean(checkTablesCmd.ExecuteScalar());
            if (!sessionsExists)
                return;

            using var checkHistoryCmd = conn.CreateCommand();
            checkHistoryCmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
                );
                """;
            var historyExists = Convert.ToBoolean(checkHistoryCmd.ExecuteScalar());

            var alreadyRecorded = false;
            if (historyExists)
            {
                using var checkRowCmd = conn.CreateCommand();
                checkRowCmd.CommandText = $"""
                    SELECT EXISTS (
                        SELECT 1 FROM "__EFMigrationsHistory"
                        WHERE "MigrationId" = '{InitialMigrationId}'
                    );
                    """;
                alreadyRecorded = Convert.ToBoolean(checkRowCmd.ExecuteScalar());
            }

            if (alreadyRecorded)
                return;

            using var baselineCmd = conn.CreateCommand();
            baselineCmd.CommandText = $"""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{InitialMigrationId}', '{EfProductVersion}')
                ON CONFLICT ("MigrationId") DO NOTHING;
                """;
            baselineCmd.ExecuteNonQuery();
        }
        finally
        {
            if (shouldClose)
                conn.Close();
        }
    }
}
