using System.Text.Json.Serialization;

namespace Levvy.Infrastructure.Asr.Models;

internal sealed class WhisperVerboseResponse
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("segments")]
    public List<WhisperSegmentDto>? Segments { get; set; }
}

internal sealed class WhisperSegmentDto
{
    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
