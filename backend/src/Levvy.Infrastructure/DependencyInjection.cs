using Hangfire;
using Hangfire.PostgreSql;
using Levvy.Application.Common.Interfaces;
using Levvy.Infrastructure.Asr;
using Levvy.Infrastructure.Authentication;
using Levvy.Infrastructure.BackgroundJobs;
using Levvy.Infrastructure.Llm;
using Levvy.Infrastructure.Meetings;
using Levvy.Infrastructure.Persistence;
using Levvy.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Levvy.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Fallback for local dev when neither ConnectionStrings:Levvy nor DATABASE_URL is set.
    /// Persistence is Npgsql-only (Vultr Managed PostgreSQL in prod, local Postgres in dev).
    /// </summary>
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=levvy;Username=levvy;Password=levvy";

    public static string ResolveConnectionString(IConfiguration cfg)
        => cfg.GetConnectionString("Levvy")
           ?? cfg["DATABASE_URL"]
           ?? DefaultConnectionString;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var connString = ResolveConnectionString(cfg);

        services.AddDbContext<LevvyDbContext>(opt => opt.UseNpgsql(connString));

        // Audit fix (H5-iii/M3): every named client gets an explicit timeout — the default 100s
        // is a live-captioning killer when awaited inline in the WebSocket hot loop.
        services.AddHttpClient("Groq", http =>
        {
            var baseUrl = cfg["Groq:BaseUrl"] ?? cfg["GROQ_BASE_URL"] ?? "https://api.groq.com/openai/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("Deepgram", http =>
        {
            var baseUrl = cfg["Deepgram:BaseUrl"] ?? cfg["DEEPGRAM_BASE_URL"] ?? "https://api.deepgram.com/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("VultrS3", http =>
        {
            // Large lecture recordings stream in both directions; allow generous time.
            http.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddHttpClient("GoogleAuth", http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("MeetingBot", http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenService>();
        services.AddScoped<IGoogleAuthVerifier>(sp =>
            new GoogleAuthVerifier(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleAuth"),
                sp.GetRequiredService<IConfiguration>()));
        services.AddScoped<IAudioStore>(sp =>
            new VultrAudioStore(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("VultrS3"),
                sp.GetRequiredService<IConfiguration>()));
        services.AddScoped<IMeetingBotConnector, GoogleMeetBotConnector>();

        services.AddScoped<GroqAsrStreamer>(sp =>
            new GroqAsrStreamer(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Groq"),
                sp.GetRequiredService<IConfiguration>()));

        services.AddScoped<DeepgramAsrStreamer>(sp =>
            new DeepgramAsrStreamer(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Deepgram"),
                sp.GetRequiredService<IConfiguration>()));

        services.AddScoped<IAsrStreamer, ResilientAsrStreamer>();

        services.AddScoped<GroqLlmClient>(sp =>
            new GroqLlmClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Groq"),
                sp.GetRequiredService<IConfiguration>()));

        services.AddScoped<ITranslator>(sp => sp.GetRequiredService<GroqLlmClient>());
        services.AddScoped<ILlmAnswerer>(sp => sp.GetRequiredService<GroqLlmClient>());

        // --- Hangfire Background Services (PostgreSql job storage, same DB as the app) ---
        services.AddHangfire(hangfireCfg =>
        {
            hangfireCfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                       .UseSimpleAssemblyNameTypeSerializer()
                       .UseRecommendedSerializerSettings()
                       .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connString));
        });

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = Math.Max(4, Environment.ProcessorCount * 2);
        });

        services.AddScoped<CaptionPersistenceJob>();
        services.AddScoped<StudyPackGenerationJob>();
        services.AddScoped<StaleSessionCleanupJob>();
        services.AddScoped<IBackgroundJobScheduler, HangfireJobScheduler>();
        services.AddHostedService<RecurringJobRegistrationService>();

        return services;
    }
}
