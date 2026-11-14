using Hangfire;
using Levvy.Api.Endpoints;
using Levvy.Api.Hubs;
using Levvy.Application;
using Levvy.Infrastructure;
using Levvy.Infrastructure.Configuration;
using Levvy.Infrastructure.Persistence;

// 1. Load environment variables from .env (repo root or backend/) before building configuration
var dotEnvValues = DotEnvLoader.LoadFromDefaultLocations();

var builder = WebApplication.CreateBuilder(args);

if (dotEnvValues.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(dotEnvValues!);
}

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// 2. Register Clean Architecture layers (Application CQRS/Events + Infrastructure EF/Groq/S3/Hangfire)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<LectureSessionHub>();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LevvyDbContext>();
    DatabaseInitializer.Initialize(db);
}

app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

app.UseHangfireDashboard("/jobs");

app.MapOpenApi();
app.MapSwaggerEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "levvy-api" }))
   .WithTags("Health")
   .WithSummary("Health check endpoint");
app.MapSessionEndpoints();
app.MapWebSocketEndpoints();

app.Run();
