namespace Irion.DuckDB.NET.Test.S3;

public static class MinioContainerExtensions
{
    public static string GetDuckDbS3Endpoint(this MinioContainer container)
    {
        var endpoint = new Uri(container.GetConnectionString());
        return endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
    }

    public static Uri GetEndpointUri(this MinioContainer container)
    {
        return new Uri(container.GetConnectionString());
    }
}
