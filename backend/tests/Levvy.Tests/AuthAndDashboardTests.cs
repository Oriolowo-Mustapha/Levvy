using Levvy.Application;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Queries;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Authentication;
using Levvy.Infrastructure.Persistence;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Levvy.Tests;

public class AuthAndDashboardTests
{
    private static (ServiceProvider Sp, LevvyDbContext Db, SqliteConnection Connection) BuildTestServices(IGoogleAuthVerifier? googleVerifier = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddApplication();

        services.AddDbContext<LevvyDbContext>(opt => opt.UseSqlite(connection));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        var cfg = new StubAuthConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "test-super-secret-jwt-key-minimum-32-chars!!",
            ["Jwt:Issuer"] = "levvy-api",
            ["Jwt:Audience"] = "levvy-web",
            ["Jwt:ExpiryMinutes"] = "60"
        });
        services.AddSingleton<IConfiguration>(cfg);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenService>();
        services.AddSingleton(googleVerifier ?? new FakeGoogleVerifier());

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<LevvyDbContext>();
        db.Database.EnsureCreated();
        return (sp, db, connection);
    }

    [Fact]
    public async Task Email_registration_login_and_jwt_validation_succeed_and_reject_duplicates()
    {
        var (sp, db, conn) = BuildTestServices();
        await using var _ = db;
        await using var __ = conn;
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var register = await mediator.Send(new RegisterUserCommand(
            "Amara",
            "Okafor",
            "amara@levvy.tech",
            "SecurePass123!",
            Role: "student",
            Institution: "University of Lagos",
            Department: "Economics",
            PreferredLanguage: "es",
            AccessibilityMode: "international",
            CaptionFontSize: "lg"));

        Assert.True(register.IsSuccess);
        Assert.Equal("Amara", register.Value.User.FirstName);
        Assert.Equal("Okafor", register.Value.User.LastName);
        Assert.Equal("es", register.Value.User.PreferredLanguage);
        Assert.Equal("international", register.Value.User.AccessibilityMode);
        Assert.Equal("lg", register.Value.User.CaptionFontSize);
        Assert.Equal(register.Value.User.Id, jwt.ValidateTokenAndGetUserId(register.Value.AccessToken));

        var duplicate = await mediator.Send(new RegisterUserCommand(
            "Amara",
            "Okafor",
            "AMARA@levvy.tech",
            "SecurePass123!"));
        Assert.True(duplicate.IsFailure);
        Assert.Equal("Auth.EmailExists", duplicate.Error.Code);

        var badLogin = await mediator.Send(new LoginUserCommand("amara@levvy.tech", "WrongPassword!"));
        Assert.True(badLogin.IsFailure);
        Assert.Equal("Auth.InvalidCredentials", badLogin.Error.Code);

        var goodLogin = await mediator.Send(new LoginUserCommand("amara@levvy.tech", "SecurePass123!"));
        Assert.True(goodLogin.IsSuccess);
        Assert.Equal(register.Value.User.Id, goodLogin.Value.User.Id);
    }

    [Fact]
    public async Task Google_auth_creates_new_user_and_logs_in_existing_user()
    {
        var (sp, db, conn) = BuildTestServices();
        await using var _ = db;
        await using var __ = conn;
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var firstSignIn = await mediator.Send(new GoogleAuthCommand(
            "valid-google-id-token",
            Role: "instructor",
            Institution: "Georgia Tech",
            Department: "CS",
            PreferredLanguage: "en",
            AccessibilityMode: "deaf_hoh"));

        Assert.True(firstSignIn.IsSuccess);
        Assert.Equal("google", firstSignIn.Value.User.AuthProvider);
        Assert.Equal("Tunde", firstSignIn.Value.User.FirstName);
        Assert.Equal("Bakare", firstSignIn.Value.User.LastName);
        Assert.Equal("deaf_hoh", firstSignIn.Value.User.AccessibilityMode);

        var secondSignIn = await mediator.Send(new GoogleAuthCommand("valid-google-id-token"));
        Assert.True(secondSignIn.IsSuccess);
        Assert.Equal(firstSignIn.Value.User.Id, secondSignIn.Value.User.Id);
    }

    [Fact]
    public async Task Profile_update_and_dashboard_aggregate_hosted_and_attended_sessions()
    {
        var (sp, db, conn) = BuildTestServices();
        await using var _ = db;
        await using var __ = conn;
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var auth = await mediator.Send(new RegisterUserCommand(
            "Kemi",
            "Adeyemi",
            "kemi@levvy.tech",
            "StrongPass!99",
            Role: "instructor"));
        var userId = auth.Value.User.Id;

        var updated = await mediator.Send(new UpdateUserProfileCommand(
            userId,
            "Kemi",
            "Adeyemi-Bello",
            Role: "instructor",
            Institution: "Lagos State University",
            Department: "Accessibility Lab",
            PreferredLanguage: "fr",
            AccessibilityMode: "deaf_hoh",
            CaptionFontSize: "xl"));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Adeyemi-Bello", updated.Value.LastName);
        Assert.Equal("fr", updated.Value.PreferredLanguage);
        Assert.Equal("xl", updated.Value.CaptionFontSize);

        var hostedSessionId = Guid.NewGuid();
        var attendedSessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Sessions.AddRange(
            new Session
            {
                Id = hostedSessionId,
                Code = "HST-101",
                Title = "Accessible Systems 101",
                HostId = userId,
                StartedAtUtc = now.AddHours(-2),
                EndedAtUtc = now.AddHours(-1),
                RecordingKey = "recordings/hst-101.webm",
                StudyPack = new StudyPack
                {
                    Id = Guid.NewGuid(),
                    SessionId = hostedSessionId,
                    Summary = "Summary bullet [05:10]",
                    GeneratedAtUtc = now.AddHours(-1),
                    Flashcards = new List<Flashcard>
                    {
                        new() { Id = Guid.NewGuid(), Front = "Q1", Back = "A1", SourceOffsetMs = 310_000 }
                    }
                }
            },
            new Session
            {
                Id = attendedSessionId,
                Code = "ATT-202",
                Title = "Microeconomics Lecture",
                HostId = Guid.NewGuid(),
                StartedAtUtc = now.AddMinutes(-20),
                Participants = new List<Participant>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        SessionId = attendedSessionId,
                        UserId = userId,
                        DisplayName = "Kemi",
                        PreferredLanguage = "fr",
                        JoinedAtUtc = now.AddMinutes(-19)
                    }
                },
                Questions = new List<Question>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        SessionId = attendedSessionId,
                        AskerId = userId,
                        Text = "Can you clarify elasticity?",
                        AskedAtUtc = now.AddMinutes(-10)
                    }
                }
            });
        await db.SaveChangesAsync();

        var dashboardResult = await mediator.Send(new GetUserDashboardQuery(userId));
        Assert.True(dashboardResult.IsSuccess);
        var dash = dashboardResult.Value;

        Assert.Equal("Kemi", dash.Profile.FirstName);
        Assert.Equal("Lagos State University", dash.Profile.Institution);
        Assert.Equal(1, dash.TotalSessionsHosted);
        Assert.Equal(1, dash.TotalSessionsAttended);
        Assert.Equal(1, dash.TotalQuestionsAsked);
        Assert.Equal(1, dash.TotalStudyPacksAvailable);
        Assert.Single(dash.HostedSessions);
        Assert.True(dash.HostedSessions[0].HasStudyPack);
        Assert.True(dash.HostedSessions[0].HasRecording);
        Assert.Single(dash.AttendedSessions);
        Assert.True(dash.AttendedSessions[0].IsLive);
        Assert.Single(dash.StudyPacks);
        Assert.Equal(1, dash.StudyPacks[0].FlashcardCount);
    }

    private sealed class FakeGoogleVerifier : IGoogleAuthVerifier
    {
        public Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct)
            => Task.FromResult(new GoogleUserPayload(
                "google-sub-12345",
                "tunde@levvy.tech",
                "Tunde",
                "Bakare",
                "https://lh3.googleusercontent.com/avatar.png"));
    }

    private sealed class StubAuthConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var val) ? val : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }
}
