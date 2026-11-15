using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Levvy.Api.Endpoints;
using Levvy.Api.Hubs;
using Levvy.Application;
using Levvy.Infrastructure;
using Levvy.Infrastructure.Configuration;
using Levvy.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// 1. Load environment variables from .env (repo root or backend/) before building configuration
var dotEnvValues = DotEnvLoader.LoadFromDefaultLocations();

var builder = WebApplication.CreateBuilder(args);

if (dotEnvValues.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(dotEnvValues!);
}

// Audit fix (C3): never run with the hardcoded dev JWT secret outside Development —
// it is committed to a public repo, so tokens would be forgeable by anyone.
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? builder.Configuration["JWT_SECRET"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("WARNING: JWT_SECRET missing/short — using the dev-only fallback secret. Never deploy like this.");
    }
    else
    {
        throw new InvalidOperationException(
            "JWT_SECRET must be configured (>= 32 chars) in non-development environments. Set it in .env or the environment.");
    }
}

// Audit fix (H2): warn loudly when Google sign-in is enabled without a client id —
// audience validation would be skipped, accepting ID tokens minted for any Google app.
var googleClientId = builder.Configuration["Google:ClientId"] ?? builder.Configuration["GOOGLE_CLIENT_ID"];
if (string.IsNullOrWhiteSpace(googleClientId) && !builder.Environment.IsDevelopment())
{
    Console.WriteLine("WARNING: GOOGLE_CLIENT_ID is not set — /auth/google will accept ID tokens from any Google OAuth app.");
}

// Audit fix (H3): restrict CORS to known frontend origins; AllowAnyOrigin only as a dev fallback.
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? builder.Configuration["CORS_ALLOWED_ORIGINS"];
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            if (!builder.Environment.IsDevelopment())
            {
                Console.WriteLine("WARNING: CORS_ALLOWED_ORIGINS unset in non-dev — allowing any origin.");
            }
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Audit fix (H1): basic per-IP rate limiting on the auth endpoints (credential stuffing / PBKDF2 CPU DoS).
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1)
        }));
});

// 2. Register Clean Architecture layers (Application CQRS/Events + Infrastructure EF/Groq/S3/Hangfire)
builder.Services.AddApplication();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(LectureSessionHub).Assembly));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<LectureSessionHub>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Levvy.Infrastructure.Authentication.WebSocketTicketStore>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LevvyDbContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();
app.UseCors();
// Audit fix (H1): activate the rate limiter registered above.
app.UseRateLimiter();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

// Audit fix (H4): explicit, deliberate dashboard authorization (token-gated; loopback in dev only).
var dashboardOptions = new DashboardOptions
{
    Authorization = [TokenDashboardAuthorizationFilter.Instance]
};
app.UseHangfireDashboard("/jobs", dashboardOptions);

app.MapOpenApi();
app.MapSwaggerEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "levvy-api" }))
   .WithTags("Health")
   .WithSummary("Health check endpoint");
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapSessionEndpoints();
app.MapWebSocketEndpoints();

app.Run();
