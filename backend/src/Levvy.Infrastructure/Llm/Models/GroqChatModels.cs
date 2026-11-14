using System.Text.Json.Serialization;

namespace Levvy.Infrastructure.Llm.Models;

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChoiceDto>? Choices { get; set; }
}

internal sealed class ChoiceDto
{
    [JsonPropertyName("message")]
    public MessageDto? Message { get; set; }
}

internal sealed class MessageDto
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal sealed class GroundedAnswerDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("receipts")]
    public List<long>? Receipts { get; set; }
}

internal sealed class StudyPackLlmDto
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("flashcards")]
    public List<FlashcardLlmDto>? Flashcards { get; set; }
}

internal sealed class FlashcardLlmDto
{
    [JsonPropertyName("front")]
    public string? Front { get; set; }

    [JsonPropertyName("back")]
    public string? Back { get; set; }

    [JsonPropertyName("sourceOffsetMs")]
    public long SourceOffsetMs { get; set; }
}
