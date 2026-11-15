using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Queries;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Levvy.Tests;

/// <summary>
/// Persistence-layer tests for SessionRepository + LevvyDbContext.
/// SQLite in-memory is used as a test-only fake relational provider so the unique
/// Code index and relational query behavior are actually enforced; the runtime
/// persistence target is Vultr Managed PostgreSQL (Npgsql) exclusively.
/// </summary>
public class SessionRepositoryTests
{
    private static (LevvyDbContext Db, SqliteConnection Connection) CreateOpenDb()
    {
        // :memory: databases die with their connection, so the connection must stay
        // open for the lifetime of the test.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<LevvyDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new LevvyDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    private static Session NewSession(string code = "K4F-8Q2") => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Title = "ECN-201 · Elasticity of demand — Lecture 7",
        HostId = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow
    };

    private static TranscriptSegment Segment(Guid sessionId, long offsetMs, string text) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        OffsetMs = offsetMs,
        Text = text,
        SourceLanguage = "en",
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task GetByCodeAsync_trims_and_uppercases_incoming_code()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var session = NewSession();
            db.Sessions.Add(session);
            db.SaveChanges();

            var repo = new SessionRepository(db);
            var found = await repo.GetByCodeAsync("  k4f-8q2  ", CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(session.Id, found!.Id);
            Assert.Equal("K4F-8Q2", found.Code);
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task Duplicate_session_code_violates_unique_index()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var repo = new SessionRepository(db);

            await repo.AddAsync(NewSession("A1B-2C3"), CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);

            await repo.AddAsync(NewSession("A1B-2C3"), CancellationToken.None);
            Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetWithTranscriptAsync_loads_full_graph_and_returns_null_for_unknown_id()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var session = NewSession();
            session.Segments.Add(Segment(session.Id, 2_507_000, "If the price of coffee climbs, demand for tea rises."));

            var questionId = Guid.NewGuid();
            session.Questions.Add(new Question
            {
                Id = questionId,
                SessionId = session.Id,
                AskerId = Guid.NewGuid(),
                Text = "What happens when coffee price climbs?",
                AskedAtUtc = DateTime.UtcNow,
                Answer = new Answer
                {
                    Id = Guid.NewGuid(),
                    QuestionId = questionId,
                    Text = "Demand for tea rises with it.",
                    Status = "answered",
                    Citations =
                    [
                        new AnswerCitation { Id = Guid.NewGuid(), OffsetMs = 2_507_000, SegmentText = "If the price of coffee climbs, demand for tea rises." }
                    ],
                    CreatedAtUtc = DateTime.UtcNow
                }
            });
            session.StudyPack = new StudyPack
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Summary = "Summary with [41:47]",
                GeneratedAtUtc = DateTime.UtcNow,
                Flashcards =
                [
                    new Flashcard { Id = Guid.NewGuid(), Front = "Substitutes?", Back = "Coffee and tea.", SourceOffsetMs = 2_507_000 }
                ]
            };

            db.Sessions.Add(session);
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var repo = new SessionRepository(db);
            var loaded = await repo.GetWithTranscriptAsync(session.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Segments);
            Assert.Equal("If the price of coffee climbs, demand for tea rises.", loaded.Segments.First().Text);

            var answer = loaded.Questions.Single().Answer;
            Assert.NotNull(answer);
            Assert.Equal("answered", answer!.Status);
            Assert.Equal(2_507_000, answer.Citations.Single().OffsetMs);

            Assert.NotNull(loaded.StudyPack);
            Assert.Equal("Summary with [41:47]", loaded.StudyPack!.Summary);
            Assert.Single(loaded.StudyPack.Flashcards);

            Assert.Null(await repo.GetWithTranscriptAsync(Guid.NewGuid(), CancellationToken.None));
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetStudyPack_query_orders_transcript_and_marks_last_line_current_when_pack_missing()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var session = NewSession();
            // Deliberately inserted out of order to verify handler ordering
            session.Segments.Add(Segment(session.Id, 2_592_000, "Second segment."));
            session.Segments.Add(Segment(session.Id, 2_507_000, "First segment."));
            db.Sessions.Add(session);
            db.SaveChanges();

            var repo = new SessionRepository(db);
            var dto = await new GetStudyPackByCodeQueryHandler(repo)
                .Handle(new GetStudyPackByCodeQuery(session.Code), CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("Study pack is being generated from the lecture transcript.", dto!.Summary);
            Assert.Empty(dto.Flashcards);

            Assert.Equal([2_507_000, 2_592_000], dto.Transcript.Select(t => t.OffsetMs));
            Assert.False(dto.Transcript[0].IsCurrent);
            Assert.True(dto.Transcript[^1].IsCurrent);
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetStudyPack_query_maps_flashcards_ordered_by_source_offset()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var session = NewSession();
            session.Segments.Add(Segment(session.Id, 2_592_000, "High substitution."));
            session.Segments.Add(Segment(session.Id, 2_507_000, "Coffee and tea."));

            session.StudyPack = new StudyPack
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Summary = "- Substitutes [41:47]\n- High substitution [43:12]",
                GeneratedAtUtc = DateTime.UtcNow,
                Flashcards =
                [
                    new Flashcard { Id = Guid.NewGuid(), Front = "Second card", Back = "Consumers switch easily.", SourceOffsetMs = 2_592_000 },
                    new Flashcard { Id = Guid.NewGuid(), Front = "First card", Back = "Demand for tea rises.", SourceOffsetMs = 2_507_000 }
                ]
            };

            db.Sessions.Add(session);
            db.SaveChanges();

            var repo = new SessionRepository(db);
            var dto = await new GetStudyPackByCodeQueryHandler(repo)
                .Handle(new GetStudyPackByCodeQuery(session.Code), CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("- Substitutes [41:47]\n- High substitution [43:12]", dto!.Summary);
            Assert.Equal([2_507_000, 2_592_000], dto.Flashcards.Select(f => f.SourceOffsetMs));
            Assert.Equal("First card", dto.Flashcards[0].Front);
            Assert.Equal("Second card", dto.Flashcards[^1].Front);
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GenerateStudyPackCommandHandler_persists_pack_flashcards_and_parts_via_ef_core()
    {
        var (db, connection) = CreateOpenDb();
        try
        {
            var session = NewSession("GEN-500");
            session.Segments.Add(Segment(session.Id, 5_000, "First concept in distributed systems."));
            session.Segments.Add(Segment(session.Id, 15_000, "Raft elects a leader using heartbeats."));
            db.Sessions.Add(session);
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var repo = new SessionRepository(db);
            var handler = new GenerateStudyPackCommandHandler(repo, new StubStudyPackAnswerer());

            var firstPackId = await handler.Handle(new GenerateStudyPackCommand(session.Id), CancellationToken.None);
            db.ChangeTracker.Clear();
            var secondPackId = await handler.Handle(new GenerateStudyPackCommand(session.Id), CancellationToken.None);

            Assert.NotEqual(Guid.Empty, firstPackId);
            Assert.Equal(firstPackId, secondPackId);

            db.ChangeTracker.Clear();
            var loaded = await new GetStudyPackByCodeQueryHandler(repo)
                .Handle(new GetStudyPackByCodeQuery("GEN-500"), CancellationToken.None);
            Assert.NotNull(loaded);
            Assert.NotEmpty(loaded!.Flashcards);
            Assert.Single(loaded.Parts);
            Assert.Contains("[00:05]", loaded.Summary);
        }
        finally
        {
            db.Dispose();
            connection.Dispose();
        }
    }

    private sealed class StubStudyPackAnswerer : ILlmAnswerer
    {
        public Task<(string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs)> AnswerFromTranscriptAsync(
            string question,
            IReadOnlyList<TranscriptSegment> transcriptSoFar,
            CancellationToken ct)
            => Task.FromResult<(string, string, IReadOnlyList<long>)>(("ok", "Answer", [5_000]));

        public Task<GeneratedStudyPackResult> GenerateStudyPackAsync(
            IReadOnlyList<TranscriptSegment> transcript,
            CancellationToken ct)
            => Task.FromResult(new GeneratedStudyPackResult(
                "- Raft overview [00:05]",
                [new GeneratedFlashcardItem("How does Raft pick a leader?", "Election timeout triggers a new term.", 15_000)],
                [new GeneratedPartItem("Raft overview", 5_000)]));
    }
}
