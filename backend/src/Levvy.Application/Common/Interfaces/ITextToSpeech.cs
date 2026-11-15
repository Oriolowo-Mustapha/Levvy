namespace Levvy.Application.Common.Interfaces;

public sealed record TextToSpeechResult(byte[] AudioBytes, string ContentType);

/// <summary>
/// Text-to-speech boundary. Implemented by Deepgram Aura TTS.
/// </summary>
public interface ITextToSpeech
{
    Task<TextToSpeechResult> SynthesizeAsync(
        string text,
        string? language = null,
        string? voiceModel = null,
        CancellationToken ct = default);
}
