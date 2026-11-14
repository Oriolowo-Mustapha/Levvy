namespace Levvy.Application.Common.Interfaces;

/// <summary>Stores raw recordings + generated packs in Vultr Object Storage.</summary>
public interface IAudioStore
{
    Task<string> SaveRecordingAsync(Guid sessionId, Stream audio, CancellationToken ct);
    Task<Stream?> OpenRecordingAsync(string key, CancellationToken ct);
}
