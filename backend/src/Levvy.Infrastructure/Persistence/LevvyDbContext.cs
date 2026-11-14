using Levvy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Levvy.Infrastructure.Persistence;

/// <summary>
/// Persistence targets Vultr Managed PostgreSQL via Npgsql only (prod-first build).
/// The connection string comes from ConnectionStrings:Levvy or the DATABASE_URL env var.
/// </summary>
public class LevvyDbContext : DbContext
{
    public LevvyDbContext(DbContextOptions<LevvyDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<StudyPack> StudyPacks => Set<StudyPack>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Session>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Segments).WithOne().HasForeignKey("SessionId");
            e.HasMany(x => x.Participants).WithOne().HasForeignKey("SessionId");
            e.HasMany(x => x.Questions).WithOne().HasForeignKey("SessionId");
        });
        b.Entity<TranscriptSegment>(e =>
        {
            // Receipt timeline queries always order by OffsetMs within a session
            e.HasIndex(x => new { x.SessionId, x.OffsetMs });
        });
        b.Entity<Answer>(e => e.HasMany(x => x.Citations).WithOne().HasForeignKey("AnswerId"));
        b.Entity<StudyPack>(e => e.HasMany(x => x.Flashcards).WithOne().HasForeignKey("StudyPackId"));
    }
}
