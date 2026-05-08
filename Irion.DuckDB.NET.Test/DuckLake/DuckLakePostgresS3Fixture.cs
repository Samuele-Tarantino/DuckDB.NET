using Irion.DuckDB.NET.Test.S3;

namespace Irion.DuckDB.NET.Test.DuckLake;

public sealed class DuckLakePostgresS3Fixture : DockerIntegrationFixture
{
    public const string BucketName = "ducklake";

    public PostgreSqlContainer PostgreSql => Environment.PostgreSql();

    public MinioContainer Minio => Environment.Minio();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder
            .AddPostgreSql()
            .AddMinio();
    }

    protected override Task InitializeServicesAsync()
    {
        return MinioBucketClient.CreateBucketIfNotExistsAsync(
            Minio.GetEndpointUri(),
            Minio.GetAccessKey(),
            Minio.GetSecretKey(),
            BucketName);
    }

    public static string CreateObjectUri(string key)
    {
        return $"s3://{BucketName}/{key}";
    }
}
