namespace Levvy.Infrastructure.Llm;

/// <summary>
/// Formats recording millisecond offsets into mm:ss receipt strings (e.g. 2592000 -> "43:12").
/// </summary>
public static class TimestampFormatter
{
    public static string FormatMmSs(long offsetMs)
    {
        var totalSeconds = Math.Max(0, offsetMs / 1000);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:D2}:{seconds:D2}";
    }
}
