using System.Net;
using System.Text;
using FluentValidation;
using Levvy.Application;
using Levvy.Application.Commands;
using Levvy.Application.Common;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.BackgroundJobs;
using Levvy.Infrastructure.Meetings;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Levvy.Tests;

/// <summary>
/// plan.md Phase 2 — Google Meet & Online Class Bot (Dev B): URL validation,
/// bot dispatch/monitor lifecycle, webhook caption persistence, auto study packs.
/// </summary>
public class OnlineClassTests
{
    // ---------- MeetingUrlParser (FluentValidation URL check) ----------

    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", "google_meet")]
    [InlineData("http://meet.google.com/xyz-abcd-efg", "google_meet")]
    [InlineData("https://us05web.zoom.us/j/81234567890?pwd=abc", "zoom")]
    [InlineData("https://teams.microsoft.com/l/meetup-join/19:meeting_abc", "teams")]
    [InlineData("https://teams.live.com/meet/9367259585212", "teams")]
    public void MeetingUrlParser_accepts_supported_platforms(string url, string expectedPlatform)
    {
        Assert.True(MeetingUrlParser.TryGetPlatform(url, out var platform));
        Assert.Equal(expectedPlatform, platform);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://evil.example.com/meet.google.com")] // lookalike host — must NOT pass (audit fix M7: host-aware parsing)
    [InlineData("https://meet.google.com.evil.example.com/abc-defg-hij")] // subdomain-of-attacker trick
    [InlineData("ftp://meet.google.com/abc-defg-hij")] // non-http(s) scheme
    public void MeetingUrlParser_rejects_unsupported_urls(string? url)
    {
        Assert.False(MeetingUrlParser.TryGetPlatform(url, out _));
    }

    [Fact]
    public async Task ConnectOnlineClass_validator_rejects_unsupported_meeting_url()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://youtube.com/watch?v=x")));
    }

    // ---------- ConnectOnlineClassCommand (dispatch flow) ----------

    [Fact]
    public async Task Connect_online_class_creates_session_with_meeting_fields_and_schedules_bot_dispatch()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var hostId = Guid.NewGuid();

        var result = await mediator.Send(new ConnectOnlineClassCommand(hostId, "Bio 101 · Genetics", "https://meet.google.com/abc-defg-hij"));

        Assert.Equal(7, result.Code.Length);
        Assert.Equal("google_meet", result.Platform);
        Assert.Equal("pending", result.BotStatus);

        var session = await repo.GetWithTranscriptAsync(result.SessionId, CancellationToken.None);
        Assert.NotNull(session);
        Assert.Equal("google_meet", session!.MeetingPlatform);
        Assert.Equal("https://meet.google.com/abc-defg-hij", session.ExternalMeetingUrl);
        Assert.Equal(hostId, session.HostId);

        Assert.Contains(result.SessionId, scheduler.DispatchBotSessionIds);
        Assert.Empty(connector.Dispatched); // bot dispatch happens in the Hangfire worker, not inline
    }

    [Fact]
    public async Task OnlineClassBotJob_dispatch_sets_bot_id_and_schedules_monitor()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        connector.QueueDispatch = new MeetingBotDispatchResult("bot-123", "joining");
        var job = new OnlineClassBotJob(repo, connector, scheduler, NullLogger<OnlineClassBotJob>.Instance);
        await job.DispatchAsync(created.SessionId, CancellationToken.None);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("bot-123", session!.ExternalBotId);
        Assert.Equal("joining", session.BotStatus);
        Assert.Contains(created.SessionId, scheduler.MonitorBotSessionIds);
    }

    [Fact]
    public async Task OnlineClassBotJob_without_api_key_marks_session_unconfigured()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        connector.QueueDispatch = new MeetingBotDispatchResult(null, "unconfigured");
        var job = new OnlineClassBotJob(repo, connector, scheduler, NullLogger<OnlineClassBotJob>.Instance);
        await job.DispatchAsync(created.SessionId, CancellationToken.None);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Null(session!.ExternalBotId);
        Assert.Equal("unconfigured", session.BotStatus); // Mode B fallback (browser tab audio)
        Assert.Empty(scheduler.MonitorBotSessionIds);   // nothing to monitor without a bot
    }

    [Fact]
    public async Task OnlineClassBotJob_monitor_reschedules_while_joining_and_stops_when_active()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        connector.QueueDispatch = new MeetingBotDispatchResult("bot-123", "joining");
        var job = new OnlineClassBotJob(repo, connector, scheduler, NullLogger<OnlineClassBotJob>.Instance);
        await job.DispatchAsync(created.SessionId, CancellationToken.None);
        scheduler.MonitorBotSessionIds.Clear();

        // Still joining -> re-schedule the monitor
        connector.QueueStatus = new MeetingBotStatusResult("joining", IsActive: false);
        await job.MonitorAsync(created.SessionId, CancellationToken.None);
        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("joining", session!.BotStatus);
        Assert.Single(scheduler.MonitorBotSessionIds);

        // Bot in-call -> stop polling
        scheduler.MonitorBotSessionIds.Clear();
        connector.QueueStatus = new MeetingBotStatusResult("active", IsActive: true);
        await job.MonitorAsync(created.SessionId, CancellationToken.None);
        session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("active", session!.BotStatus);
        Assert.Empty(scheduler.MonitorBotSessionIds);
    }

    // ---------- ConnectMeetCommand (attach to existing session) ----------

    [Fact]
    public async Task Connect_meet_attaches_url_to_existing_session_and_schedules_dispatch()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var created = await mediator.Send(new CreateSessionCommand("ECN-201 · Elasticity", Guid.NewGuid()));

        var result = await mediator.Send(new ConnectMeetCommand(created.SessionId, "https://teams.microsoft.com/l/meetup-join/19:meeting_abc"));

        Assert.Equal("teams", result.Platform);
        Assert.Equal("pending", result.BotStatus);
        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("teams", session!.MeetingPlatform);
        Assert.Contains(created.SessionId, scheduler.DispatchBotSessionIds);
    }

    [Fact]
    public async Task Connect_meet_rejects_unknown_session()
    {
        var (mediator, _, _, _) = BuildMediator();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            mediator.Send(new ConnectMeetCommand(Guid.NewGuid(), "https://meet.google.com/abc-defg-hij")));
    }

    [Fact]
    public async Task Connect_meet_does_not_re_dispatch_when_bot_already_active()
    {
        var (mediator, repo, scheduler, connector) = BuildMediator();
        var created = await mediator.Send(new CreateSessionCommand("ECN-201 · Elasticity", Guid.NewGuid()));
        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        session!.MeetingPlatform = "google_meet";
        session.ExternalMeetingUrl = "https://meet.google.com/abc-defg-hij";
        session.BotStatus = "active";
        session.ExternalBotId = "bot-live";

        var result = await mediator.Send(new ConnectMeetCommand(created.SessionId, "https://meet.google.com/abc-defg-hij"));

        Assert.Equal("active", result.BotStatus);
        Assert.Empty(scheduler.DispatchBotSessionIds);
    }

    // ---------- Meet webhook (captions + lifecycle) ----------

    [Fact]
    public async Task Webhook_caption_publishes_CaptionFinalized_and_persists_segment()
    {
        var (mediator, repo, scheduler, _) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        var result = await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "caption", Event: null, OffsetMs: 2_507_000,
            Text: "If the price of coffee climbs, demand for tea rises with it.",
            Language: "en", BotId: "bot-123"));

        Assert.Equal("caption_accepted", result.Status);

        // The CaptionFinalized seam enqueued the persistence job exactly like the hot audio pipe
        var (jobSessionId, jobOffsetMs, jobText) = Assert.Single(scheduler.CaptionJobs);
        Assert.Equal(created.SessionId, jobSessionId);
        Assert.Equal(2_507_000, jobOffsetMs);

        var captionJob = new CaptionPersistenceJob(repo, NullLogger<CaptionPersistenceJob>.Instance);
        await captionJob.ExecuteAsync(jobSessionId, jobOffsetMs, jobText, "en", CancellationToken.None);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        var segment = Assert.Single(session!.Segments);
        Assert.Equal("If the price of coffee climbs, demand for tea rises with it.", segment.Text);
        Assert.Equal("active", session.BotStatus); // captions flowing -> bot in call
    }

    [Fact]
    public async Task Webhook_call_ended_ends_session_and_enqueues_study_pack_job()
    {
        var (mediator, repo, scheduler, _) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        // caption first so the transcript is non-empty
        await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "caption", null, 2_507_000, "If the price of coffee climbs, demand for tea rises with it.", "en", null));

        var result = await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "event", "call_ended", null, null, null, "bot-123"));

        Assert.Equal("ended", result.Status);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.NotNull(session!.EndedAtUtc);
        Assert.Equal("ended", session.BotStatus);
        Assert.Contains(created.SessionId, scheduler.StudyPackSessionIds); // auto-generated cited study pack
    }

    [Fact]
    public async Task Webhook_bot_lifecycle_events_update_bot_status()
    {
        var (mediator, repo, scheduler, _) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        await mediator.Send(new IngestMeetingWebhookCommand(created.SessionId, "event", "bot_active", null, null, null, "bot-123"));
        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("active", session!.BotStatus);

        await mediator.Send(new IngestMeetingWebhookCommand(created.SessionId, "event", "bot_failed", null, null, null, null));
        session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("failed", session!.BotStatus);

        // Unknown event types are ignored, not fatal
        var result = await mediator.Send(new IngestMeetingWebhookCommand(created.SessionId, "event", "something_new", null, null, null, null));
        Assert.Equal("ignored", result.Status);
    }

    [Fact]
    public async Task Webhook_MeetingBaasV2_event_names_mark_active_and_completed_ended()
    {
        var (mediator, repo, scheduler, _) = BuildMediator();
        var created = await mediator.Send(new ConnectOnlineClassCommand(Guid.NewGuid(), "Bio 101", "https://meet.google.com/abc-defg-hij"));

        var activeResult = await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "event", "in_call_recording", null, null, null, "mb-bot-42"));
        Assert.Equal("ok", activeResult.Status);

        var session = await repo.GetWithTranscriptAsync(created.SessionId, CancellationToken.None);
        Assert.Equal("active", session!.BotStatus);
        Assert.Equal("mb-bot-42", session.ExternalBotId);

        await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "caption", null, 15_000, "Elasticity measures responsiveness.", "en", "mb-bot-42"));

        var completedResult = await mediator.Send(new IngestMeetingWebhookCommand(
            created.SessionId, "event", "bot.completed", null, null, null, "mb-bot-42"));
        Assert.Equal("ended", completedResult.Status);
        Assert.Equal("ended", session.BotStatus);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Contains(created.SessionId, scheduler.StudyPackSessionIds);
    }

    [Fact]
    public async Task GoogleMeetBotConnector_MeetingBaasV2_dispatches_and_polls_status()
    {
        var handler = new StubHttpHandler(
            (HttpStatusCode.Created, """{"success":true,"data":{"bot_id":"mb-bot-42","status":"created"}}"""),
            (HttpStatusCode.OK, """{"success":true,"data":{"status":"in_call_recording"}}"""),
            (HttpStatusCode.OK, """{"success":true,"data":{"status":"call_ended"}}"""));

        var connector = BuildConnector(handler, new()
        {
            ["MeetingBot:ApiKey"] = "mb-test-key",
            ["MeetingBot:ApiBaseUrl"] = "https://api.meetingbaas.com/v2" // without trailing slash
        });

        var sessionId = Guid.NewGuid();
        var dispatch = await connector.DispatchBotAsync(sessionId, "https://meet.google.com/abc-defg-hij", "Levvy Note-Taker", CancellationToken.None);
        Assert.Equal("mb-bot-42", dispatch.ExternalBotId);
        Assert.Equal("joining", dispatch.Status);

        var active = await connector.GetBotStatusAsync(sessionId, "mb-bot-42", CancellationToken.None);
        Assert.Equal("active", active.Status);
        Assert.True(active.IsActive);

        var ended = await connector.GetBotStatusAsync(sessionId, "mb-bot-42", CancellationToken.None);
        Assert.Equal("ended", ended.Status);
        Assert.False(ended.IsActive);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("https://api.meetingbaas.com/v2/bots", handler.Requests[0].Uri);
        Assert.Equal("mb-test-key", handler.Requests[0].ApiKeyHeader);
        Assert.Null(handler.Requests[0].AuthorizationHeader);
        Assert.Contains("\"extra\"", handler.Requests[0].Body);
        Assert.Equal("https://api.meetingbaas.com/v2/bots/mb-bot-42/status", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task GoogleMeetBotConnector_RecallAi_uses_bearer_and_v1_routes()
    {
        var handler = new StubHttpHandler(
            (HttpStatusCode.Created, """{"id":"recall-99"}"""),
            (HttpStatusCode.OK, """{"status":{"code":"in_call"}}"""));

        var connector = BuildConnector(handler, new()
        {
            ["MeetingBot:ApiKey"] = "recall-key",
            ["MeetingBot:ApiBaseUrl"] = "https://api.recall.ai/api/v1/"
        });

        var sessionId = Guid.NewGuid();
        var dispatch = await connector.DispatchBotAsync(sessionId, "https://meet.google.com/abc-defg-hij", "Levvy Note-Taker", CancellationToken.None);
        Assert.Equal("recall-99", dispatch.ExternalBotId);
        Assert.Equal("joining", dispatch.Status);

        var status = await connector.GetBotStatusAsync(sessionId, "recall-99", CancellationToken.None);
        Assert.Equal("active", status.Status);
        Assert.True(status.IsActive);

        Assert.Equal("https://api.recall.ai/api/v1/bot", handler.Requests[0].Uri);
        Assert.Equal("Bearer recall-key", handler.Requests[0].AuthorizationHeader);
        Assert.Null(handler.Requests[0].ApiKeyHeader);
        Assert.Contains("\"metadata\"", handler.Requests[0].Body);
        Assert.Equal("https://api.recall.ai/api/v1/bot/recall-99", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task GoogleMeetBotConnector_GetBotStatus_handles_non_success_http_gracefully()
    {
        var handler = new StubHttpHandler((HttpStatusCode.InternalServerError, "upstream error"));
        var connector = BuildConnector(handler, new()
        {
            ["MeetingBot:ApiKey"] = "mb-test-key"
        });

        var status = await connector.GetBotStatusAsync(Guid.NewGuid(), "mb-bot-42", CancellationToken.None);
        Assert.Equal("joining", status.Status);
        Assert.False(status.IsActive);
    }

    private static GoogleMeetBotConnector BuildConnector(StubHttpHandler handler, Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new GoogleMeetBotConnector(
            new StubHttpClientFactory(handler),
            config,
            NullLogger<GoogleMeetBotConnector>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpHandler(params (HttpStatusCode Code, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body)> _responses = new(responses);
        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;
            var apiKeyHeader = request.Headers.TryGetValues("x-meeting-baas-api-key", out var values)
                ? values.FirstOrDefault()
                : null;

            Requests.Add(new RecordedRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                apiKeyHeader,
                body));

            var (code, respBody) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "{}");

            return new HttpResponseMessage(code)
            {
                Content = new StringContent(respBody, Encoding.UTF8, "application/json")
            };
        }

        public sealed record RecordedRequest(
            string Method,
            string Uri,
            string? AuthorizationHeader,
            string? ApiKeyHeader,
            string? Body);
    }

    [Fact]
    public async Task Webhook_rejects_caption_without_text_and_unknown_session()
    {
        var (mediator, _, _, _) = BuildMediator();

        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new IngestMeetingWebhookCommand(Guid.NewGuid(), "caption", null, 1000, "", "en", null)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            mediator.Send(new IngestMeetingWebhookCommand(Guid.NewGuid(), "event", "call_ended", null, null, null, null)));
    }

    private static (IMediator Mediator, InMemorySessionRepository Repo, SpyJobScheduler Scheduler, FakeMeetingBotConnector Connector) BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var repo = new InMemorySessionRepository();
        var scheduler = new SpyJobScheduler();
        var connector = new FakeMeetingBotConnector();
        services.AddSingleton<ISessionRepository>(repo);
        services.AddSingleton<IBackgroundJobScheduler>(scheduler);
        services.AddSingleton<IMeetingBotConnector>(connector);

        var sp = services.BuildServiceProvider();
        // Resolve from the root provider: repo/scheduler/connector are singletons and
        // MediatR's IMediator is transient, so no scope lifetime is needed here.
        var mediator = sp.GetRequiredService<IMediator>();
        return (mediator, repo, scheduler, connector);
    }

    private sealed class FakeMeetingBotConnector : IMeetingBotConnector
    {
        public MeetingBotDispatchResult? QueueDispatch { get; set; }
        public MeetingBotStatusResult? QueueStatus { get; set; }
        public List<(Guid SessionId, string Url)> Dispatched { get; } = new();

        public Task<MeetingBotDispatchResult> DispatchBotAsync(Guid sessionId, string meetingUrl, string botName, CancellationToken ct)
        {
            Dispatched.Add((sessionId, meetingUrl));
            return Task.FromResult(QueueDispatch ?? new MeetingBotDispatchResult("bot-default", "joining"));
        }

        public Task<MeetingBotStatusResult> GetBotStatusAsync(Guid sessionId, string externalBotId, CancellationToken ct)
            => Task.FromResult(QueueStatus ?? new MeetingBotStatusResult("active", IsActive: true));

        public Task RemoveBotAsync(string externalBotId, CancellationToken ct) => Task.CompletedTask;
    }
}
