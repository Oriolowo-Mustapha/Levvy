using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Levvy.Infrastructure.Meetings;

/// <summary>
/// Meeting-bot provider integration (plan.md Phase 2, Mode A).
/// Supports Meeting BaaS API v2 (https://api.meetingbaas.com/v2/, default) and
/// Recall.ai API v1 (https://api.recall.ai/api/v1/, selected automatically when
/// MeetingBot:ApiBaseUrl contains "recall.ai"). Configured via MeetingBot:ApiKey
/// (MEETING_BOT_API_KEY); without a key the connector reports "unconfigured" so
/// callers fall back to Mode B (browser tab audio).
/// </summary>
public sealed class GoogleMeetBotConnector(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GoogleMeetBotConnector> logger) : IMeetingBotConnector
{
    public const string DefaultApiBaseUrl = "https://api.meetingbaas.com/v2/";
    public const string DefaultWebhookBaseUrl = "https://levvy.tech";

    private string? ApiKey => configuration["MeetingBot:ApiKey"] ?? configuration["MEETING_BOT_API_KEY"];

    private string ApiBaseUrl
    {
        get
        {
            var raw = configuration["MeetingBot:ApiBaseUrl"] ?? configuration["MEETING_BOT_API_BASE_URL"];
            var normalized = string.IsNullOrWhiteSpace(raw) ? DefaultApiBaseUrl : raw.Trim();
            return normalized.EndsWith('/') ? normalized : normalized + "/";
        }
    }

    private string WebhookBaseUrl
    {
        get
        {
            var raw = configuration["MeetingBot:WebhookBaseUrl"] ?? configuration["MEETING_BOT_WEBHOOK_BASE_URL"];
            var normalized = string.IsNullOrWhiteSpace(raw) ? DefaultWebhookBaseUrl : raw.Trim();
            return normalized.TrimEnd('/');
        }
    }

    public async Task<MeetingBotDispatchResult> DispatchBotAsync(
        Guid sessionId,
        string meetingUrl,
        string botName,
        CancellationToken ct)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new MeetingBotDispatchResult(null, "unconfigured");

        var baseUrl = ApiBaseUrl;
        var isRecall = IsRecallAi(baseUrl);

        try
        {
            var endpoint = isRecall ? "bot" : "bots";
            object payload = isRecall
                ? new { meeting_url = meetingUrl, bot_name = botName, metadata = new { session_id = sessionId } }
                : new
                {
                    meeting_url = meetingUrl,
                    bot_name = botName,
                    transcription_enabled = true,
                    webhook_url = $"{WebhookBaseUrl}/sessions/{sessionId}/meet-webhook",
                    extra = new { session_id = sessionId }
                };

            using var request = CreateRequest(HttpMethod.Post, endpoint, apiKey, isRecall, JsonContent.Create(payload));
            var client = CreateClient(baseUrl);
            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Meeting bot dispatch for session {SessionId} failed with HTTP {StatusCode}.",
                    sessionId,
                    (int)response.StatusCode);
                return new MeetingBotDispatchResult(null, "failed");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var botId = ExtractBotId(doc.RootElement);

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

        var baseUrl = ApiBaseUrl;
        var isRecall = IsRecallAi(baseUrl);

        try
        {
            var endpoint = isRecall ? $"bot/{externalBotId}" : $"bots/{externalBotId}/status";
            using var request = CreateRequest(HttpMethod.Get, endpoint, apiKey, isRecall);
            var client = CreateClient(baseUrl);
            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Meeting bot status poll for {BotId} returned HTTP {StatusCode}; keeping joining state.",
                    externalBotId,
                    (int)response.StatusCode);
                return new MeetingBotStatusResult("joining", IsActive: false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var providerCode = ExtractStatusCode(doc.RootElement);

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

        var baseUrl = ApiBaseUrl;
        var isRecall = IsRecallAi(baseUrl);
        var client = CreateClient(baseUrl);

        try
        {
            if (isRecall)
            {
                using var deleteReq = CreateRequest(HttpMethod.Delete, $"bot/{externalBotId}", apiKey, isRecall: true);
                await client.SendAsync(deleteReq, ct);
                return;
            }

            // Meeting BaaS v2 uses POST /v2/bots/{id}/leave for in-call bots; fall back to DELETE if not yet joined.
            using var emptyJson = new StringContent("{}", Encoding.UTF8, "application/json");
            using var leaveReq = CreateRequest(HttpMethod.Post, $"bots/{externalBotId}/leave", apiKey, isRecall: false, emptyJson);
            using var leaveResp = await client.SendAsync(leaveReq, ct);

            if (!leaveResp.IsSuccessStatusCode)
            {
                using var deleteReq = CreateRequest(HttpMethod.Delete, $"bots/{externalBotId}", apiKey, isRecall: false);
                await client.SendAsync(deleteReq, ct);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Best-effort bot removal for {BotId} failed; ignoring.", externalBotId);
        }
    }

    private HttpClient CreateClient(string baseUrl)
    {
        var client = httpClientFactory.CreateClient("MeetingBot");
        var targetUri = new Uri(baseUrl);
        if (client.BaseAddress != targetUri)
            client.BaseAddress = targetUri;
        return client;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativeEndpoint,
        string apiKey,
        bool isRecall,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, relativeEndpoint)
        {
            Content = content
        };

        if (isRecall)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        else
            request.Headers.TryAddWithoutValidation("x-meeting-baas-api-key", apiKey);

        return request;
    }

    private static bool IsRecallAi(string baseUrl)
        => baseUrl.Contains("recall.ai", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts bot ID from Meeting BaaS v2 (data.bot_id / data.id) or Recall.ai v1 (id / bot_id) payloads.</summary>
    internal static string? ExtractBotId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
        {
            var nested = TryGetNonEmptyString(dataEl, "bot_id") ?? TryGetNonEmptyString(dataEl, "id");
            if (nested is not null)
                return nested;
        }

        return TryGetNonEmptyString(root, "bot_id") ?? TryGetNonEmptyString(root, "id");
    }

    /// <summary>Extracts status string from Meeting BaaS v2 (data.status / data.status.code) or Recall.ai v1 (status / status.code) payloads.</summary>
    internal static string? ExtractStatusCode(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
        {
            var nested = TryReadStatusFromObject(dataEl);
            if (nested is not null)
                return nested;
        }

        return TryReadStatusFromObject(root);
    }

    private static string? TryReadStatusFromObject(JsonElement obj)
    {
        if (!obj.TryGetProperty("status", out var statusEl))
            return null;

        if (statusEl.ValueKind == JsonValueKind.String)
        {
            var val = statusEl.GetString();
            return string.IsNullOrWhiteSpace(val) ? null : val;
        }

        if (statusEl.ValueKind == JsonValueKind.Object)
            return TryGetNonEmptyString(statusEl, "code");

        return null;
    }

    private static string? TryGetNonEmptyString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
            return null;

        var value = el.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Maps Meeting BaaS v2 and Recall.ai status codes onto Session.BotStatus values.</summary>
    internal static (string Status, bool IsActive) MapStatus(string? providerCode)
        => providerCode?.Trim().ToLowerInvariant() switch
        {
            "in_call" or "participating" or "in_call_recording" or "in_call_not_recording"
                or "recording_permission_allowed" or "recording" or "active" => ("active", true),
            "created" or "queued" or "joining" or "joining_call" or "in_waiting_room"
                or "waiting_room" or "ready" or "scheduled" or "pending" => ("joining", false),
            "call_ended" or "analyzing" or "transcribing" or "completed" or "done" or "ended" => ("ended", false),
            "fatal" or "failed" or "error" or "recording_permission_denied"
                or "rejected" or "kicked" => ("failed", false),
            _ => ("joining", false) // unknown code: keep polling rather than killing the monitor loop
        };
}
