namespace Irion.DuckDB.NET.Test.S3;

public static class MinioContainerExtensions
{
    private const string TestcontainersHostOverrideEnvironmentVariable = "TESTCONTAINERS_HOST_OVERRIDE";

    public static string GetDuckDbS3Endpoint(this MinioContainer container)
    {
        var endpoint = container.GetEndpointUri();
        return endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
    }

    public static Uri GetEndpointUri(this MinioContainer container)
    {
        var endpoint = new Uri(container.GetConnectionString());
        var hostOverride = Environment.GetEnvironmentVariable(TestcontainersHostOverrideEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(hostOverride))
        {
            return endpoint;
        }

        var builder = new UriBuilder(endpoint)
        {
            Host = hostOverride,
        };

        return builder.Uri;
    }
}
