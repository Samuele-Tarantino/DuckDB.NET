using Irion.DuckDB.NET.Test.Infrastructure;

namespace Irion.DuckDB.NET.Test.PostgreSql;

public sealed class PostgreSqlIntegrationFixture : DockerIntegrationFixture
{
    public PostgreSqlContainer PostgreSql => Environment.PostgreSql();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder.AddPostgreSql();
    }

    protected override async Task InitializeServicesAsync()
    {
        await PostgreSql.ExecScriptAsync(
            """
            CREATE TABLE IF NOT EXISTS integration_numbers
            (
                id integer PRIMARY KEY,
                name text NOT NULL
            );

            INSERT INTO integration_numbers (id, name)
            VALUES (1, 'one')
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;
            """);
    }
}
