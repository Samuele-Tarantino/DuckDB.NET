namespace Irion.DuckDB.NET.Test.S3;

public sealed class S3ParquetIntegrationFixture : DockerIntegrationFixture
{
    public const string BucketName = "duckdb-parquet";

    public MinioContainer Minio => Environment.Minio();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder.AddMinio();
    }

    protected override Task InitializeServicesAsync()
    {
        return MinioBucketClient.CreateBucketIfNotExistsAsync(
            Minio.GetEndpointUri(),
            Minio.GetAccessKey(),
            Minio.GetSecretKey(),
            BucketName);
    }

    public string CreateDuckDbS3SetupSql()
    {
        return
            $"""
            INSTALL httpfs;
            LOAD httpfs;
            SET s3_region = 'us-east-1';
            SET s3_access_key_id = {SqlLiterals.DuckDbString(Minio.GetAccessKey())};
            SET s3_secret_access_key = {SqlLiterals.DuckDbString(Minio.GetSecretKey())};
            SET s3_endpoint = {SqlLiterals.DuckDbString(Minio.GetDuckDbS3Endpoint())};
            SET s3_url_style = 'path';
            SET s3_use_ssl = false;
            """;
    }

    public static string CreateObjectUri(string key)
    {
        return $"s3://{BucketName}/{key}";
    }
}
