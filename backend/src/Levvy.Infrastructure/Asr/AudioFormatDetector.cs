namespace Levvy.Infrastructure.Asr;

/// <summary>
/// Detects audio container format (WebM, MP4/M4A, MP3, WAV, OGG, FLAC) from magic bytes so
/// recordings from iOS/macOS Safari (audio/mp4) and uploaded audio files work reliably with
/// Deepgram Nova-3, Groq Whisper, and browser range playback across all devices.
/// </summary>
public static class AudioFormatDetector
{
    public readonly record struct AudioFormat(string ContentType, string Extension);

    public static AudioFormat DetectFromBytes(ReadOnlySpan<byte> header, string? fallbackContentType = null)
    {
        if (header.Length >= 4)
        {
            // EBML / WebM / Matroska: 1A 45 DF A3
            if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
                return new AudioFormat("audio/webm", ".webm");

            // OGG: "OggS"
            if (header[0] == (byte)'O' && header[1] == (byte)'g' && header[2] == (byte)'g' && header[3] == (byte)'S')
                return new AudioFormat("audio/ogg", ".ogg");

            // FLAC: "fLaC"
            if (header[0] == (byte)'f' && header[1] == (byte)'L' && header[2] == (byte)'a' && header[3] == (byte)'C')
                return new AudioFormat("audio/flac", ".flac");

            // WAV: "RIFF"...."WAVE"
            if (header.Length >= 12 &&
                header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                header[8] == (byte)'W' && header[9] == (byte)'A' && header[10] == (byte)'V' && header[11] == (byte)'E')
                return new AudioFormat("audio/wav", ".wav");

            // MP3 ID3 tag: "ID3"
            if (header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3')
                return new AudioFormat("audio/mpeg", ".mp3");

            // Raw MPEG frame sync (MP3)
            if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                return new AudioFormat("audio/mpeg", ".mp3");
        }

        // ISO Base Media File Format (MP4 / M4A): "ftyp" at offset 4
        if (header.Length >= 8 &&
            header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
            return new AudioFormat("audio/mp4", ".m4a");

        return FromMimeType(fallbackContentType);
    }

    public static AudioFormat FromMimeType(string? contentType)
    {
        var normalized = contentType?.Split(';')[0].Trim().ToLowerInvariant();
        return normalized switch
        {
            "audio/mp4" or "audio/x-m4a" or "audio/m4a" or "video/mp4" => new AudioFormat("audio/mp4", ".m4a"),
            "audio/mpeg" or "audio/mp3" => new AudioFormat("audio/mpeg", ".mp3"),
            "audio/wav" or "audio/wave" or "audio/x-wav" => new AudioFormat("audio/wav", ".wav"),
            "audio/ogg" or "application/ogg" => new AudioFormat("audio/ogg", ".ogg"),
            "audio/flac" or "audio/x-flac" => new AudioFormat("audio/flac", ".flac"),
            "audio/webm" or "video/webm" or _ => new AudioFormat("audio/webm", ".webm")
        };
    }

    public static AudioFormat DetectFromSeekableStream(Stream stream, string? fallbackContentType = null)
    {
        if (!stream.CanSeek)
            return FromMimeType(fallbackContentType);

        var start = stream.Position;
        Span<byte> header = stackalloc byte[16];
        var read = stream.Read(header);
        stream.Position = start;
        return DetectFromBytes(header[..read], fallbackContentType);
    }
}
