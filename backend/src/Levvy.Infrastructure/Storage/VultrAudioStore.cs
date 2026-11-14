using System.Security.Cryptography;
using System.Text;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Storage;

/// <summary>
/// Stores raw lecture recordings in Vultr Object Storage (S3-compatible) when Storage:Endpoint &amp; keys are configured,
/// with automatic local filesystem fallback under AppContext.BaseDirectory/recordings so dev/demo never breaks.
/// </summary>
public class VultrAudioStore(HttpClient http, IConfiguration cfg) : IAudioStore
{
    private readonly string _localDir = Path.Combine(AppContext.BaseDirectory, "recordings");

    public async Task<string> SaveRecordingAsync(Guid sessionId, Stream audio, CancellationToken ct)
    {
        var key = $"sessions/{sessionId:N}/recording.webm";
        Directory.CreateDirectory(_localDir);
        var localPath = Path.Combine(_localDir, $"{sessionId:N}.webm");

        using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await audio.CopyToAsync(fs, ct);
        }

        var endpoint = cfg["Storage:Endpoint"];
        var bucket = cfg["Storage:Bucket"];
        var accessKey = cfg["Storage:AccessKey"];
        var secretKey = cfg["Storage:SecretKey"];

        if (!string.IsNullOrWhiteSpace(endpoint) &&
            !string.IsNullOrWhiteSpace(bucket) &&
            !string.IsNullOrWhiteSpace(accessKey) &&
            !string.IsNullOrWhiteSpace(secretKey))
        {
            var bytes = await File.ReadAllBytesAsync(localPath, ct);
            await UploadS3ObjectAsync(endpoint, bucket, key, accessKey, secretKey, bytes, ct);
        }

        return key;
    }

    public Task<Stream?> OpenRecordingAsync(string key, CancellationToken ct)
    {
        // Extract sessionId from "sessions/{sessionId}/recording.webm" or direct filename
        var parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sessionPart = parts.Length >= 2 ? parts[1] : Path.GetFileNameWithoutExtension(key);
        var localPath = Path.Combine(_localDir, $"{sessionPart}.webm");

        if (File.Exists(localPath))
        {
            Stream s = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(s);
        }

        return Task.FromResult<Stream?>(null);
    }

    private async Task UploadS3ObjectAsync(
        string endpoint,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        byte[] payload,
        CancellationToken ct)
    {
        var host = endpoint.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var region = cfg["Storage:Region"] ?? "ewr1";
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payload));
        var canonicalUri = $"/{bucket}/{key}";
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"PUT\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = GetSignatureKey(secretKey, dateStamp, region, "s3");
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));
        var authHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        using var req = new HttpRequestMessage(HttpMethod.Put, $"https://{host}{canonicalUri}")
        {
            Content = new ByteArrayContent(payload)
        };
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("Authorization", authHeader);

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(regionName));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(serviceName));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }
}
