using System.Net.Http.Json;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Meetings;

/// <summary>
/// Recall.ai-style external meeting-bot provider integration (plan.md Phase 2, Mode A).
/// POST /bot spawns a "Levvy Note-Taker", GET /bot/{id} polls its lifecycle status,
/// DELETE /bot/{id} removes it. Configured via MeetingBot:ApiKey (MEETING_BOT_API_KEY);
/// without a key the connector reports "unconfigured" so callers fall back to Mode B.
/// </summary>
public sealed class GoogleMeetBotConnector(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GoogleMeetBotConnector> logger) : IMeetingBotConnector
{
    private const string DefaultApiBaseUrl = "https://api.recall.ai/api/v1/";

    private string? ApiKey => configuration["MeetingBot:ApiKey"];
    private string ApiBaseUrl => configuration["MeetingBot:ApiBaseUrl"] ?? DefaultApiBaseUrl;

    public async Task<MeetingBotDispatchResult> DispatchBotAsync(
        Guid sessionId,
        string meetingUrl,
        string botName,
        CancellationToken ct)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new MeetingBotDispatchResult(null, "unconfigured");

        try
        {
            var client = CreateClient(apiKey);
            var response = await client.PostAsJsonAsync("bot", new
            {
                meeting_url = meetingUrl,
                bot_name = botName,
                metadata = new { session_id = sessionId }
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Meeting bot dispatch for session {SessionId} failed with HTTP {StatusCode}.",
                    sessionId,
                    (int)response.StatusCode);
                return new MeetingBotDispatchResult(null, "failed");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var botId = doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;

            return botId is not null
                ? new MeetingBotDispatchResult(botId, "joining")
                : new MeetingBotDispatchResult(null, "failed");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Meeting bot dispatch for session {SessionId} threw; marking failed.", sessionId);
            return new MeetingBotDispatchResult(null, "failed");
        }
    }

    public async Task<MeetingBotStatusResult> GetBotStatusAsync(
        Guid sessionId,
        string externalBotId,
        CancellationToken ct)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new MeetingBotStatusResult("unconfigured", IsActive: false);

        try
        {
            var client = CreateClient(apiKey);
            using var doc = await JsonDocument.ParseAsync(
                await (await client.GetAsync($"bot/{externalBotId}", ct)).Content.ReadAsStreamAsync(ct),
                cancellationToken: ct);

            var providerCode = doc.RootElement.TryGetProperty("status", out var statusEl) &&
                               statusEl.ValueKind == JsonValueKind.Object &&
                               statusEl.TryGetProperty("code", out var codeEl) &&
                               codeEl.ValueKind == JsonValueKind.String
                ? codeEl.GetString()
                : null;

            var (status, isActive) = MapStatus(providerCode);
            return new MeetingBotStatusResult(status, isActive);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Meeting bot status poll for {BotId} threw; keeping current status.", externalBotId);
            return new MeetingBotStatusResult("joining", IsActive: false);
        }
    }

    public async Task RemoveBotAsync(string externalBotId, CancellationToken ct)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        try
        {
            var client = CreateClient(apiKey);
            await client.DeleteAsync($"bot/{externalBotId}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Best-effort bot removal for {BotId} failed; ignoring.", externalBotId);
        }
    }

    private HttpClient CreateClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient("MeetingBot");
        client.BaseAddress = new Uri(ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
        return client;
    }

    /// <summary>Maps provider status codes onto Session.BotStatus values.</summary>
    private static (string Status, bool IsActive) MapStatus(string? providerCode) => providerCode switch
    {
        "in_call" or "participating" => ("active", true),
        "joining" or "ready" or "scheduled" or "pending" => ("joining", false),
        "call_ended" or "analyzing" or "done" => ("ended", false),
        "fatal" or "failed" or "error" => ("failed", false),
        _ => ("joining", false) // unknown code: keep polling rather than killing the monitor loop
    };
}
