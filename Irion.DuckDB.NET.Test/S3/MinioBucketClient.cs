namespace Irion.DuckDB.NET.Test.S3;

public static class MinioBucketClient
{
    private const string Service = "s3";
    private const string Region = "us-east-1";
    private static readonly HttpClient HttpClient = new();

    public static async Task CreateBucketIfNotExistsAsync(
        Uri endpoint,
        string accessKey,
        string secretKey,
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint, bucketName));
        Sign(request, accessKey, secretKey, bucketName, payload: string.Empty);

        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Could not create MinIO bucket '{bucketName}'. Status: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }

    private static void Sign(
        HttpRequestMessage request,
        string accessKey,
        string secretKey,
        string bucketName,
        string payload)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = Sha256Hex(payload);
        var host = request.RequestUri!.IsDefaultPort
            ? request.RequestUri.Host
            : $"{request.RequestUri.Host}:{request.RequestUri.Port}";

        request.Headers.TryAddWithoutValidation("Host", host);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

        var canonicalRequest =
            $"""
            PUT
            /{bucketName}

            host:{host}
            x-amz-content-sha256:{payloadHash}
            x-amz-date:{amzDate}

            host;x-amz-content-sha256;x-amz-date
            {payloadHash}
            """;

        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var stringToSign =
            $"""
            AWS4-HMAC-SHA256
            {amzDate}
            {credentialScope}
            {Sha256Hex(canonicalRequest)}
            """;

        var signingKey = GetSignatureKey(secretKey, dateStamp, Region, Service);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature={signature}");
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{key}"), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Sha256Hex(string value)
    {
        return ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
