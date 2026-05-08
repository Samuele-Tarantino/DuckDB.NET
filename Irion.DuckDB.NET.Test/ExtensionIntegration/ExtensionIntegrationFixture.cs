using Irion.DuckDB.NET.Test.S3;

namespace Irion.DuckDB.NET.Test.ExtensionIntegration;

public sealed class ExtensionIntegrationFixture : DockerIntegrationFixture
{
    public const string BucketName = "extension-integration";
    public const string SqlServerDatabaseName = "ExtensionIntegration";

    public PostgreSqlContainer PostgreSql => Environment.PostgreSql();

    public MinioContainer Minio => Environment.Minio();

    public MsSqlContainer SqlServer => Environment.SqlServer();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder
            .AddPostgreSql()
            .AddMinio()
            .AddSqlServerExpress();
    }

    protected override async Task InitializeServicesAsync()
    {
        await SqlServer.ExecScriptAsync(
            $"""
            IF DB_ID(N'{SqlServerDatabaseName}') IS NULL
            BEGIN
                CREATE DATABASE [{SqlServerDatabaseName}];
            END
            """);

        await MinioBucketClient.CreateBucketIfNotExistsAsync(
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
