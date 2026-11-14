namespace Levvy.Api.Endpoints;

public sealed record CreateSessionRequest(string Title, Guid? HostId);
public sealed record JoinSessionRequest(string? DisplayName, string? PreferredLanguage, Guid? UserId = null);
public sealed record AskQuestionRequest(string Text, Guid? AskerId);
