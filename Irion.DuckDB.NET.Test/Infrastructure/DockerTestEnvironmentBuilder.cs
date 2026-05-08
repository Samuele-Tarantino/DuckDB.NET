namespace Irion.DuckDB.NET.Test.Infrastructure;

public sealed class DockerTestEnvironmentBuilder
{
    private readonly Dictionary<string, Func<IContainer>> registrations = new(StringComparer.OrdinalIgnoreCase);

    public DockerTestEnvironmentBuilder AddContainer<TContainer>(string name, Func<TContainer> containerFactory)
        where TContainer : class, IContainer
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(containerFactory);

        registrations[name] = containerFactory;
        return this;
    }

    public DockerTestEnvironmentBuilder AddContainer(
        string name,
        string image,
        Func<ContainerBuilder, ContainerBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image);

        var builder = new ContainerBuilder(image);
        builder = configure?.Invoke(builder) ?? builder;

        return AddContainer(name, () => builder.Build());
    }

    public DockerTestEnvironmentBuilder AddPostgreSql(
        string name = DockerServiceNames.PostgreSql,
        Func<PostgreSqlBuilder, PostgreSqlBuilder>? configure = null)
    {
        var builder = new PostgreSqlBuilder("postgres:17.5-alpine")
            .WithDatabase("duckdb")
            .WithUsername("duckdb")
            .WithPassword("duckdb");

        builder = configure?.Invoke(builder) ?? builder;

        return AddContainer(name, () => builder.Build());
    }

    public DockerTestEnvironmentBuilder AddMinio(
        string name = DockerServiceNames.Minio,
        Func<MinioBuilder, MinioBuilder>? configure = null)
    {
        var builder = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z")
            .WithUsername("minioadmin")
            .WithPassword("minioadmin");

        builder = configure?.Invoke(builder) ?? builder;

        return AddContainer(name, () => builder.Build());
    }

    public DockerTestEnvironmentBuilder AddSqlServerExpress(
        string name = DockerServiceNames.SqlServer,
        Func<MsSqlBuilder, MsSqlBuilder>? configure = null)
    {
        var builder = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Irion.Strong.Password!2026")
            .WithEnvironment("MSSQL_PID", "Express");

        builder = configure?.Invoke(builder) ?? builder;

        return AddContainer(name, () => builder.Build());
    }

    public DockerTestEnvironment Build()
    {
        return new DockerTestEnvironment(registrations);
    }
}
