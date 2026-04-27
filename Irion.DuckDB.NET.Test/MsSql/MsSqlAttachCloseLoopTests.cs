namespace Irion.DuckDB.NET.Test.MsSql;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class MsSqlAttachCloseLoopTests(SqlServerIntegrationFixture fixture)
{
    private const int DefaultAttachCloseIterations = 1000;

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "mssql")]
    public async Task DuckDb_can_open_attach_query_and_close_mssql_repeatedly()
    {
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(SqlServerIntegrationFixture.DatabaseName));

        var iterations = GetAttachCloseIterations();

        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            var alias = $"sqlserver_loop_{iteration}";
            var schema = SqlServerIntegrationFixture.SchemasUnderTest[(iteration - 1) % SqlServerIntegrationFixture.SchemasUnderTest.Length];

            var rowCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
                setup:
                $"""
                INSTALL mssql FROM community;
                LOAD mssql;
                ATTACH {sqlServerConnectionString} AS {alias} (TYPE MSSQL);
                """,
                query:
                $"""
                SELECT COUNT(*)
                FROM {alias}.{schema}.table_0001
                """);

            rowCount.Should().Be(0, $"schema {schema} and table_0001 should be queryable on iteration {iteration}");
        }
    }

    private static int GetAttachCloseIterations()
    {
        var rawValue = Environment.GetEnvironmentVariable("IRION_DUCKDB_MSSQL_ATTACH_CLOSE_ITERATIONS");

        return int.TryParse(rawValue, out var value) && value > 0
            ? value
            : DefaultAttachCloseIterations;
    }
}
