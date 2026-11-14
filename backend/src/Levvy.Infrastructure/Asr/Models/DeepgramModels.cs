using System.Text.Json.Serialization;

namespace Levvy.Infrastructure.Asr.Models;

internal sealed class DeepgramResponse
{
    [JsonPropertyName("results")]
    public DeepgramResults? Results { get; set; }
}

internal sealed class DeepgramResults
{
    [JsonPropertyName("utterances")]
    public List<DeepgramUtterance>? Utterances { get; set; }

    [JsonPropertyName("channels")]
    public List<DeepgramChannel>? Channels { get; set; }
}

internal sealed class DeepgramUtterance
{
    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }
}

internal sealed class DeepgramChannel
{
    [JsonPropertyName("detected_language")]
    public string? DetectedLanguage { get; set; }

    [JsonPropertyName("alternatives")]
    public List<DeepgramAlternative>? Alternatives { get; set; }
}

internal sealed class DeepgramAlternative
{
    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }

    [JsonPropertyName("words")]
    public List<DeepgramWord>? Words { get; set; }
}

internal sealed class DeepgramWord
{
    [JsonPropertyName("word")]
    public string? Word { get; set; }

    [JsonPropertyName("punctuated_word")]
    public string? PunctuatedWord { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }
}
