namespace Irion.DuckDB.NET.Test.DuckLake;

public sealed class DuckLakePostgresFileSystemFixture : DockerIntegrationFixture
{
    public PostgreSqlContainer PostgreSql => Environment.PostgreSql();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder.AddPostgreSql();
    }
}
