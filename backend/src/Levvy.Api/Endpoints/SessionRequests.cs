namespace Levvy.Api.Endpoints;

public sealed record CreateSessionRequest(string Title, Guid? HostId);
public sealed record JoinSessionRequest(string? DisplayName, string? PreferredLanguage, Guid? UserId = null);
public sealed record AskQuestionRequest(string Text, Guid? AskerId);
public sealed record SpeakTextRequest(string Text, string? Language = null, string? VoiceModel = null);
public sealed record ConnectOnlineClassRequest(string Title, string MeetingUrl, Guid? HostId = null);
public sealed record ConnectMeetRequest(string MeetingUrl);
public sealed record MeetWebhookRequest(
    string? Type,
    string? Event,
    long? OffsetMs,
    string? Text,
    string? Language,
    string? BotId,
    System.Text.Json.JsonElement? Data = null,
    Guid? SessionId = null);
