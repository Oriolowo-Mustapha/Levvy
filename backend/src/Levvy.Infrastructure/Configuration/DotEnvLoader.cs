namespace Levvy.Infrastructure.Configuration;

/// <summary>
/// Loads environment variables from a .env file at startup and maps flat .env keys
/// (e.g. GROQ_API_KEY, DATABASE_URL) to both OS environment variables and .NET hierarchical config keys.
/// </summary>
public static class DotEnvLoader
{
    private static readonly Dictionary<string, string> KeyMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DATABASE_PROVIDER"] = "Database__Provider",
        ["DATABASE_URL"] = "ConnectionStrings__Levvy",
        ["GROQ_API_KEY"] = "Groq__ApiKey",
        ["GROQ_BASE_URL"] = "Groq__BaseUrl",
        ["GROQ_ANSWER_MODEL"] = "Groq__AnswerModel",
        ["GROQ_FALLBACK_MODEL"] = "Groq__FallbackModel",
        ["GROQ_ASR_MODEL"] = "Groq__AsrModel",
        ["DEEPGRAM_API_KEY"] = "Deepgram__ApiKey",
        ["DEEPGRAM_BASE_URL"] = "Deepgram__BaseUrl",
        ["STORAGE_ENDPOINT"] = "Storage__Endpoint",
        ["STORAGE_REGION"] = "Storage__Region",
        ["STORAGE_BUCKET"] = "Storage__Bucket",
        ["STORAGE_ACCESS_KEY"] = "Storage__AccessKey",
        ["STORAGE_SECRET_KEY"] = "Storage__SecretKey"
    };

    public static IReadOnlyDictionary<string, string> LoadFromDefaultLocations(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                return LoadFile(candidate);
            }

            var backendCandidate = Path.Combine(dir.FullName, "backend", ".env");
            if (File.Exists(backendCandidate))
            {
                return LoadFile(backendCandidate);
            }

            dir = dir.Parent;
        }

        return new Dictionary<string, string>();
    }

    public static IReadOnlyDictionary<string, string> LoadFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new Dictionary<string, string>();

        return ParseAndApply(File.ReadAllLines(filePath));
    }

    public static IReadOnlyDictionary<string, string> ParseAndApply(IEnumerable<string> lines, bool setProcessEnv = true)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line[7..].Trim();

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            parsed[key] = value;

            if (setProcessEnv)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            if (KeyMappings.TryGetValue(key, out var mappedDoubleUnderscore))
            {
                var colonKey = mappedDoubleUnderscore.Replace("__", ":");
                parsed[colonKey] = value;
                if (setProcessEnv)
                {
                    Environment.SetEnvironmentVariable(mappedDoubleUnderscore, value);
                }
            }
        }

        return parsed;
    }
}
