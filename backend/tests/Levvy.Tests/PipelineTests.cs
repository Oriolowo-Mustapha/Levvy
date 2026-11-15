using FluentValidation;
using Levvy.Application;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using Levvy.Domain.Abstractions;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.BackgroundJobs;
using Levvy.Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Levvy.Tests;

public class PipelineTests
{
    [Fact]
    public void Application_di_registers_mediator_and_validators()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void DotEnvLoader_parses_dotenv_lines_and_maps_to_hierarchical_configuration_keys()
    {
        var lines = new[]
        {
            "# Comment line",
            "DATABASE_PROVIDER=postgres",
            "DATABASE_URL=\"Host=vultr.db;Database=levvy\"",
            "export GROQ_API_KEY='gsk_test_123'",
            "GROQ_ANSWER_MODEL=gpt-oss-120b"
        };

        var map = DotEnvLoader.ParseAndApply(lines, setProcessEnv: false);

        Assert.Equal("postgres", map["DATABASE_PROVIDER"]);
        Assert.Equal("postgres", map["Database:Provider"]);
        Assert.Equal("Host=vultr.db;Database=levvy", map["ConnectionStrings:Levvy"]);
        Assert.Equal("gsk_test_123", map["Groq:ApiKey"]);
        Assert.Equal("gpt-oss-120b", map["Groq:AnswerModel"]);
    }

    [Fact]
    public void Result_pattern_enforces_success_and_failure_invariants()
    {
        var ok = Result.Success(42);
        Assert.True(ok.IsSuccess);
        Assert.Equal(42, ok.Value);

        var fail = Result.Failure<int>(new Error("Session.NotFound", "Session was not found."));
        Assert.True(fail.IsFailure);
        Assert.Equal("Session.NotFound", fail.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = fail.Value);
    }

    [Fact]
    public async Task End_to_end_session_workflow_dispatches_hangfire_jobs_and_executes_workers()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var repo = new InMemorySessionRepository();
        var scheduler = new SpyJobScheduler();
        services.AddSingleton<ISessionRepository>(repo);
        services.AddSingleton<ILlmAnswerer, FakeGroundedAnswerer>();
        services.AddSingleton<IBackgroundJobScheduler>(scheduler);

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 1. Create session
        var created = await mediator.Send(new CreateSessionCommand("ECN-201 · Elasticity", Guid.NewGuid()));
        Assert.Equal(7, created.Code.Length);

        // 2. Join session
        var joined = await mediator.Send(new JoinSessionCommand(created.Code, "Amara", "es"));
        Assert.Equal(created.SessionId, joined.SessionId);

        // 3. Publish CaptionFinalized -> enqueues Hangfire job + execute job worker
        await mediator.Publish(new CaptionFinalized(
            created.SessionId,
            2_507_000,
            "If the price of coffee climbs, demand for tea rises with it.",
            "en"));

        Assert.Single(scheduler.CaptionJobs);
        var captionJob = new CaptionPersistenceJob(repo, NullLogger<CaptionPersistenceJob>.Instance);
        await captionJob.ExecuteAsync(
            created.SessionId,
            2_507_000,
            "If the price of coffee climbs, demand for tea rises with it.",
            "en",
            CancellationToken.None);

        // 4. Ask Levvy mid-lecture question
        var answer = await mediator.Send(new AskQuestionCommand(created.Code, joined.ParticipantId, "What happens when coffee price climbs?"));
        Assert.Equal("ok", answer.Status);
        Assert.Equal(new long[] { 2_507_000 }, answer.ReceiptOffsetsMs);

        // 5. End session -> publishes SessionEnded -> enqueues Hangfire StudyPack job + execute worker
        await mediator.Send(new EndSessionCommand(created.SessionId));
        Assert.Contains(created.SessionId, scheduler.StudyPackSessionIds);

        var packJob = new StudyPackGenerationJob(mediator, NullLogger<StudyPackGenerationJob>.Instance);
        await packJob.ExecuteAsync(created.SessionId, CancellationToken.None);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.NotNull(session?.EndedAtUtc);
        Assert.NotNull(session?.StudyPack);
        Assert.Single(session!.StudyPack!.Flashcards);
        Assert.Equal(2_507_000, session.StudyPack.Flashcards.First().SourceOffsetMs);
    }

    [Fact]
    public async Task Validation_behavior_rejects_empty_question_before_handler()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<ILlmAnswerer, FakeGroundedAnswerer>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new AskQuestionCommand("K4F-8Q2", Guid.NewGuid(), "")));
    }

    private sealed class FakeGroundedAnswerer : ILlmAnswerer
    {
        public Task<(string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs)> AnswerFromTranscriptAsync(
            string question,
            IReadOnlyList<TranscriptSegment> transcriptSoFar,
            CancellationToken ct)
        {
            if (transcriptSoFar.Count == 0)
                return Task.FromResult(("not_covered", "Not covered yet.", (IReadOnlyList<long>)Array.Empty<long>()));

            return Task.FromResult(("ok", "Demand for tea rises with it.", (IReadOnlyList<long>)new[] { transcriptSoFar[0].OffsetMs }));
        }

        public Task<GeneratedStudyPackResult> GenerateStudyPackAsync(
            IReadOnlyList<TranscriptSegment> transcript,
            CancellationToken ct)
        {
            var offset = transcript.FirstOrDefault()?.OffsetMs ?? 0;
            return Task.FromResult(new GeneratedStudyPackResult(
                "Summary citing [41:47]",
                new[] { new GeneratedFlashcardItem("Substitutes?", "Coffee and tea", offset) },
                Array.Empty<GeneratedPartItem>()));
        }
    }
}
