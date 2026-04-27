namespace Irion.DuckDB.NET.Test.MsSql;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class MsSqlTypeCoverageTests(SqlServerIntegrationFixture fixture)
{
    public static TheoryData<string, string, string> SqlServerToDuckDbTypes()
    {
        return new TheoryData<string, string, string>
        {
            { "bit", "bit_value", "value = true" },
            { "tinyint", "tinyint_value", "value = 255" },
            { "smallint", "smallint_value", "value = -12345" },
            { "int", "int_value", "value = 123456789" },
            { "bigint", "bigint_value", "value = 922337203685477580" },
            { "decimal", "decimal_value", "value = 12345.6789" },
            { "numeric", "numeric_value", "value = 123456.789012" },
            { "real", "real_value", "round(value::DOUBLE, 2) = 1.25" },
            { "float", "float_value", "value = 2.5" },
            { "money", "money_value", "value = 123.45" },
            { "smallmoney", "smallmoney_value", "value = 67.89" },
            { "char", "char_value", "trim(value) = 'abc'" },
            { "varchar", "varchar_value", "value = 'varchar-value'" },
            { "nchar", "nchar_value", "trim(value) = 'def'" },
            { "nvarchar", "nvarchar_value", "value = 'nvarchar-value'" },
            { "binary", "binary_value", "hex(value) = '01020304'" },
            { "varbinary", "varbinary_value", "hex(value) = '05060708'" },
            { "date", "date_value", "value = DATE '2026-04-24'" },
            { "time", "time_value", "value::VARCHAR LIKE '12:34:56%'" },
            { "datetime", "datetime_value", "value::VARCHAR LIKE '2026-04-24 12:34:56%'" },
            { "datetime2", "datetime2_value", "value::VARCHAR LIKE '2026-04-24 12:34:56%'" },
            { "smalldatetime", "smalldatetime_value", "value::VARCHAR LIKE '2026-04-24 12:34%'" },
            { "datetimeoffset", "datetimeoffset_value", "value::VARCHAR IS NOT NULL" },
            { "uniqueidentifier", "uniqueidentifier_value", "value = '11111111-2222-3333-4444-555555555555'" },
        };
    }

    public static TheoryData<string, string, string, string> DuckDbToSqlServerTypes()
    {
        return new TheoryData<string, string, string, string>
        {
            { "boolean", "BOOLEAN", "true", "value = true" },
            { "tinyint", "TINYINT", "127", "value = 127" },
            { "smallint", "SMALLINT", "-12345", "value = -12345" },
            { "integer", "INTEGER", "123456789", "value = 123456789" },
            { "bigint", "BIGINT", "922337203685477580", "value = 922337203685477580" },
            { "float", "FLOAT", "1.25", "round(value::DOUBLE, 2) = 1.25" },
            { "double", "DOUBLE", "2.5", "value = 2.5" },
            { "decimal", "DECIMAL(18, 4)", "12345.6789", "value = 12345.6789" },
            { "varchar", "VARCHAR", "'duckdb-mssql'", "value = 'duckdb-mssql'" },
            { "blob", "BLOB", "unhex('01020304')", "hex(value) = '01020304'" },
            { "date", "DATE", "DATE '2026-04-24'", "value = DATE '2026-04-24'" },
            { "time", "TIME", "TIME '12:34:56.123456'", "value::VARCHAR LIKE '12:34:56%'" },
            { "timestamp", "TIMESTAMP", "TIMESTAMP '2026-04-24 12:34:56.123456'", "value::VARCHAR LIKE '2026-04-24 12:34:56%'" },
            { "uuid", "UUID", "'11111111-2222-3333-4444-555555555555'", "value = '11111111-2222-3333-4444-555555555555'" },
        };
    }

    [Theory]
    [MemberData(nameof(SqlServerToDuckDbTypes))]
    [Trait("Category", "Integration")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "mssql")]
    public async Task DuckDb_can_read_sql_server_type(
        string sqlServerType,
        string columnName,
        string predicate)
    {
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(SqlServerIntegrationFixture.DatabaseName));
        var alias = $"sqlserver_read_{ToIdentifierSuffix(sqlServerType)}";

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            INSTALL mssql FROM community;
            LOAD mssql;
            ATTACH {sqlServerConnectionString} AS {alias} (TYPE MSSQL);
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM
            (
                SELECT {columnName} AS value
                FROM {alias}.dbo.sqlserver_all_types
                WHERE id = 1
            ) sqlserver_value
            WHERE {predicate}
            """);

        matchingRows.Should().Be(1, $"SQL Server {sqlServerType} should be readable through the DuckDB mssql extension");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "mssql")]
    public async Task DuckDb_can_read_sql_server_xml_type()
    {
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(SqlServerIntegrationFixture.DatabaseName));
        const string alias = "sqlserver_read_xml";

        var xmlValue = await DuckDbTestQuery.ExecuteQueryAsync<string>(
            setup:
            $"""
            INSTALL mssql FROM community;
            LOAD mssql;
            ATTACH {sqlServerConnectionString} AS {alias} (TYPE MSSQL);
            """,
            query:
            $"""
            SELECT xml_value::VARCHAR
            FROM {alias}.dbo.sqlserver_all_types
            WHERE id = 1
            """);

        xmlValue.Should().Be("<root><value>duckdb-mssql</value></root>");
    }

    [Theory]
    [MemberData(nameof(DuckDbToSqlServerTypes))]
    [Trait("Category", "Integration")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "mssql")]
    public async Task DuckDb_can_create_insert_and_read_mssql_table_for_duckdb_type(
        string duckDbTypeName,
        string duckDbType,
        string insertedValue,
        string predicate)
    {
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(SqlServerIntegrationFixture.DatabaseName));
        var typeSuffix = ToIdentifierSuffix(duckDbTypeName);
        var alias = $"sqlserver_write_{typeSuffix}";
        var tableName = $"duckdb_type_{typeSuffix}";

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            INSTALL mssql FROM community;
            LOAD mssql;
            ATTACH {sqlServerConnectionString} AS {alias} (TYPE MSSQL);

            DROP TABLE IF EXISTS {alias}.dbo.{tableName};
            CREATE TABLE {alias}.dbo.{tableName} (value {duckDbType});
            INSERT INTO {alias}.dbo.{tableName} VALUES ({insertedValue});
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM {alias}.dbo.{tableName}
            WHERE {predicate}
            """);

        matchingRows.Should().Be(1, $"DuckDB {duckDbTypeName} should create, insert, and read through the MSSQL extension");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "mssql")]
    public async Task DuckDb_can_create_insert_and_read_mssql_table_for_duckdb_timestamp_with_time_zone()
    {
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(SqlServerIntegrationFixture.DatabaseName));
        const string alias = "sqlserver_write_timestamp_tz";
        const string tableName = "duckdb_type_timestamp_with_time_zone";

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            INSTALL mssql FROM community;
            LOAD mssql;
            ATTACH {sqlServerConnectionString} AS {alias} (TYPE MSSQL);

            DROP TABLE IF EXISTS {alias}.dbo.{tableName};
            CREATE TABLE {alias}.dbo.{tableName} AS
            SELECT TIMESTAMPTZ '2026-04-24 12:34:56.123456+02:00' AS value;
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM {alias}.dbo.{tableName}
            WHERE value = TIMESTAMPTZ '2026-04-24 12:34:56.123456+02:00'
            """);

        matchingRows.Should().Be(1, "DuckDB TIMESTAMP WITH TIME ZONE should map to SQL Server DATETIMEOFFSET(7)");
    }

    private static string ToIdentifierSuffix(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
    }
}
