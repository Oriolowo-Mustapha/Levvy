using Hangfire;
using Hangfire.PostgreSql;
using Levvy.Application.Common.Interfaces;
using Levvy.Infrastructure.Asr;
using Levvy.Infrastructure.BackgroundJobs;
using Levvy.Infrastructure.Llm;
using Levvy.Infrastructure.Persistence;
using Levvy.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Levvy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dbProvider = (cfg["Database:Provider"] ?? cfg["DATABASE_PROVIDER"] ?? "sqlite").Trim().ToLowerInvariant();
        var connString = cfg.GetConnectionString("Levvy") ?? cfg["DATABASE_URL"] ?? "Data Source=levvy.db";

        services.AddDbContext<LevvyDbContext>(opt =>
        {
            if (dbProvider == "postgres")
                opt.UseNpgsql(connString);
            else
                opt.UseSqlite(connString);
        });

        services.AddHttpClient("Groq", http =>
        {
            var baseUrl = cfg["Groq:BaseUrl"] ?? cfg["GROQ_BASE_URL"] ?? "https://api.groq.com/openai/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            http.BaseAddress = new Uri(baseUrl);
        });

        services.AddHttpClient("Deepgram", http =>
        {
            var baseUrl = cfg["Deepgram:BaseUrl"] ?? cfg["DEEPGRAM_BASE_URL"] ?? "https://api.deepgram.com/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            http.BaseAddress = new Uri(baseUrl);
        });

        services.AddHttpClient("VultrS3");

        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IAudioStore>(sp =>
            new VultrAudioStore(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("VultrS3"),
                sp.GetRequiredService<IConfiguration>()));

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

        // --- Hangfire Background Services ---
        services.AddHangfire(hangfireCfg =>
        {
            hangfireCfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                       .UseSimpleAssemblyNameTypeSerializer()
                       .UseRecommendedSerializerSettings();

            if (dbProvider == "postgres" && !connString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                hangfireCfg.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connString));
            }
            else
            {
                hangfireCfg.UseInMemoryStorage();
            }
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
