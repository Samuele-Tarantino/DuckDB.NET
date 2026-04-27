using Irion.DuckDB.NET.Test.Infrastructure;

namespace Irion.DuckDB.NET.Test.PostgreSql;

public sealed class PostgreSqlExtensionSmokeTests(PostgreSqlIntegrationFixture fixture) : IClassFixture<PostgreSqlIntegrationFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("DuckDBExtension", "postgres")]
    public async Task DuckDb_can_query_postgres_container()
    {
        var postgresConnectionString = SqlLiterals.DuckDbString(fixture.PostgreSql.GetDuckDbConnectionString());

        var name = await DuckDbTestQuery.ExecuteQueryAsync<string>(
            setup:
            $"""
            INSTALL postgres;
            LOAD postgres;
            ATTACH {postgresConnectionString} AS pg (TYPE POSTGRES);
            """,
            query:
            """
            SELECT name
            FROM pg.public.integration_numbers
            WHERE id = 1
            """);

        name.Should().Be("one");
    }
}
