using System.Net;
using System.Security.Cryptography;
using System.Text;
using Levvy.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Levvy.Infrastructure.Storage;

/// <summary>
/// Stores raw lecture recordings in Vultr Object Storage (S3-compatible) when Storage:Endpoint &amp; keys are configured,
/// with automatic local filesystem fallback under AppContext.BaseDirectory/recordings so dev/demo never breaks.
/// Reads resolve to the local copy first, then fall back to a signed S3 GET so receipt playback
/// works in production where only the object storage copy exists.
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

        if (IsS3Configured(out var endpoint, out var bucket, out var accessKey, out var secretKey))
        {
            var bytes = await File.ReadAllBytesAsync(localPath, ct);
            using var resp = await SendSignedS3RequestAsync(
                HttpMethod.Put, endpoint!, bucket!, key, accessKey!, secretKey!, bytes, ct);
            resp.EnsureSuccessStatusCode();
        }

        return key;
    }

    public async Task<Stream?> OpenRecordingAsync(string key, CancellationToken ct)
    {
        var localPath = GetLocalPath(key);
        if (File.Exists(localPath))
        {
            return new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // Local copy missing (e.g. fresh deploy) — resolve from Vultr Object Storage.
        if (!IsS3Configured(out var endpoint, out var bucket, out var accessKey, out var secretKey))
            return null;

        try
        {
            using var resp = await SendSignedS3RequestAsync(
                HttpMethod.Get, endpoint!, bucket!, key, accessKey!, secretKey!, payload: null, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            resp.EnsureSuccessStatusCode();

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return new MemoryStream(bytes, writable: false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendSignedS3RequestAsync(
        HttpMethod method,
        string endpoint,
        string bucket,
        string key,
        string accessKey,
        string secretKey,
        byte[]? payload,
        CancellationToken ct)
    {
        var host = endpoint.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var region = cfg["Storage:Region"] ?? "ewr1";
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = now.ToString("yyyyMMdd");

        // GET signs an empty-body hash; PUT signs the actual payload hash.
        var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payload ?? []));
        var canonicalUri = $"/{bucket}/{key}";
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var credentialScope = $"{dateStamp}/{region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = GetSignatureKey(secretKey, dateStamp, region, "s3");
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));
        var authHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        using var req = new HttpRequestMessage(method, $"https://{host}{canonicalUri}");
        if (payload is not null)
        {
            req.Content = new ByteArrayContent(payload);
        }
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("Authorization", authHeader);

        return await http.SendAsync(req, ct);
    }

    private bool IsS3Configured(
        out string? endpoint, out string? bucket, out string? accessKey, out string? secretKey)
    {
        endpoint = cfg["Storage:Endpoint"];
        bucket = cfg["Storage:Bucket"];
        accessKey = cfg["Storage:AccessKey"];
        secretKey = cfg["Storage:SecretKey"];

        return !string.IsNullOrWhiteSpace(endpoint) &&
               !string.IsNullOrWhiteSpace(bucket) &&
               !string.IsNullOrWhiteSpace(accessKey) &&
               !string.IsNullOrWhiteSpace(secretKey);
    }

    private string GetLocalPath(string key)
    {
        // Extract sessionId from "sessions/{sessionId}/recording.webm" or direct filename
        var parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sessionPart = parts.Length >= 2 ? parts[1] : Path.GetFileNameWithoutExtension(key);
        return Path.Combine(_localDir, $"{sessionPart}.webm");
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
