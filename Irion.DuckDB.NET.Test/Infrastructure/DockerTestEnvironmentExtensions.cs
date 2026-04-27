namespace Irion.DuckDB.NET.Test.Infrastructure;

public static class DockerTestEnvironmentExtensions
{
    public static PostgreSqlContainer PostgreSql(this DockerTestEnvironment environment, string name = DockerServiceNames.PostgreSql)
    {
        return environment.Get<PostgreSqlContainer>(name);
    }

    public static MinioContainer Minio(this DockerTestEnvironment environment, string name = DockerServiceNames.Minio)
    {
        return environment.Get<MinioContainer>(name);
    }

    public static MsSqlContainer SqlServer(this DockerTestEnvironment environment, string name = DockerServiceNames.SqlServer)
    {
        return environment.Get<MsSqlContainer>(name);
    }
}
