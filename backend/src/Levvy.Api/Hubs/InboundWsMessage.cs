using System.Text.Json.Serialization;

namespace Levvy.Api.Hubs;

internal sealed class InboundWsMessage
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("offsetMs")]
    public long? OffsetMs { get; set; }
}
