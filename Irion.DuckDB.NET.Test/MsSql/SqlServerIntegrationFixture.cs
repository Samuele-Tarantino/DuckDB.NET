namespace Irion.DuckDB.NET.Test.MsSql;

public sealed class SqlServerIntegrationFixture : DockerIntegrationFixture
{
    public const string DatabaseName = "DuckDbNetIntegration";
    public const int TableCount = 100;
    public const int SchemaCount = 100;

    private const int MinColumnCount = 10;
    private const int MaxColumnCount = 50;
    private const int BatchSize = 25;

    public static readonly string[] SchemasUnderTest =
    [
        "db001",
        "db002",
        "db003",
    ];

    public MsSqlContainer SqlServer => Environment.SqlServer();

    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder.AddSqlServerExpress();
    }

    protected override async Task InitializeServicesAsync()
    {
        await SqlServer.ExecScriptAsync(
            $"""
            IF DB_ID(N'{DatabaseName}') IS NULL
            BEGIN
                CREATE DATABASE [{DatabaseName}];
            END
            """);

        await ExecuteBatchesAsync(GenerateSchemaStatements());
        await ExecuteBatchesAsync(GenerateDboTableStatements());
        await ExecuteBatchesAsync(GenerateSqlServerTypeTableStatements());

        foreach (var schema in SchemasUnderTest)
        {
            await ExecuteBatchesAsync(GenerateCopyDboToSchemaStatements(schema));
        }
    }

    private async Task ExecuteBatchesAsync(IEnumerable<string> statements)
    {
        foreach (var batch in statements.Chunk(BatchSize))
        {
            await SqlServer.ExecScriptAsync(
                $"""
                USE [{DatabaseName}];
                {string.Join(System.Environment.NewLine, batch)}
                """);
        }
    }

    private static IEnumerable<string> GenerateSchemaStatements()
    {
        for (var schemaIndex = 1; schemaIndex <= SchemaCount; schemaIndex++)
        {
            var schemaName = GetSchemaName(schemaIndex);

            yield return
                $"""
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schemaName}')
                BEGIN
                    EXEC(N'CREATE SCHEMA [{schemaName}]');
                END;
                """;
        }
    }

    private static IEnumerable<string> GenerateDboTableStatements()
    {
        for (var tableIndex = 1; tableIndex <= TableCount; tableIndex++)
        {
            var tableName = GetTableName(tableIndex);
            var columns = GenerateColumns(tableIndex);

            yield return
                $"""
                IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[{tableName}]
                    (
                        [id] int NOT NULL,
                        {string.Join($",{System.Environment.NewLine}        ", columns)}
                    );
                END;
                """;
        }
    }

    private static IEnumerable<string> GenerateCopyDboToSchemaStatements(string schemaName)
    {
        for (var tableIndex = 1; tableIndex <= TableCount; tableIndex++)
        {
            var tableName = GetTableName(tableIndex);

            yield return
                $"""
                IF OBJECT_ID(N'[{schemaName}].[{tableName}]', N'U') IS NULL
                BEGIN
                    SELECT TOP (0) *
                    INTO [{schemaName}].[{tableName}]
                    FROM [dbo].[{tableName}];
                END;
                """;
        }
    }

    private static IEnumerable<string> GenerateSqlServerTypeTableStatements()
    {
        yield return
            """
            DROP TABLE IF EXISTS [dbo].[sqlserver_all_types];

            CREATE TABLE [dbo].[sqlserver_all_types]
            (
                [id] int NOT NULL,
                [bit_value] bit NULL,
                [tinyint_value] tinyint NULL,
                [smallint_value] smallint NULL,
                [int_value] int NULL,
                [bigint_value] bigint NULL,
                [decimal_value] decimal(18, 4) NULL,
                [numeric_value] numeric(20, 6) NULL,
                [real_value] real NULL,
                [float_value] float NULL,
                [money_value] money NULL,
                [smallmoney_value] smallmoney NULL,
                [char_value] char(5) NULL,
                [varchar_value] varchar(50) NULL,
                [nchar_value] nchar(5) NULL,
                [nvarchar_value] nvarchar(50) NULL,
                [binary_value] binary(4) NULL,
                [varbinary_value] varbinary(8) NULL,
                [date_value] date NULL,
                [time_value] time(7) NULL,
                [datetime_value] datetime NULL,
                [datetime2_value] datetime2(7) NULL,
                [smalldatetime_value] smalldatetime NULL,
                [datetimeoffset_value] datetimeoffset(7) NULL,
                [uniqueidentifier_value] uniqueidentifier NULL,
                [xml_value] xml NULL
            );

            INSERT INTO [dbo].[sqlserver_all_types]
            (
                [id],
                [bit_value],
                [tinyint_value],
                [smallint_value],
                [int_value],
                [bigint_value],
                [decimal_value],
                [numeric_value],
                [real_value],
                [float_value],
                [money_value],
                [smallmoney_value],
                [char_value],
                [varchar_value],
                [nchar_value],
                [nvarchar_value],
                [binary_value],
                [varbinary_value],
                [date_value],
                [time_value],
                [datetime_value],
                [datetime2_value],
                [smalldatetime_value],
                [datetimeoffset_value],
                [uniqueidentifier_value],
                [xml_value]
            )
            VALUES
            (
                1,
                1,
                255,
                -12345,
                123456789,
                922337203685477580,
                12345.6789,
                123456.789012,
                CAST(1.25 AS real),
                CAST(2.5 AS float),
                123.45,
                67.89,
                'abc',
                'varchar-value',
                N'def',
                N'nvarchar-value',
                0x01020304,
                0x05060708,
                '2026-04-24',
                '12:34:56.1234567',
                '2026-04-24T12:34:56',
                '2026-04-24T12:34:56.1234567',
                '2026-04-24T12:34:00',
                '2026-04-24T12:34:56.1234567+02:00',
                '11111111-2222-3333-4444-555555555555',
                '<root><value>duckdb-mssql</value></root>'
            );
            """;
    }

    private static IEnumerable<string> GenerateColumns(int tableIndex)
    {
        var columnCount = MinColumnCount + (tableIndex * 37 % (MaxColumnCount - MinColumnCount + 1));

        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            yield return $"[col_{columnIndex:000}] {GetColumnType(tableIndex, columnIndex)} NULL";
        }
    }

    private static string GetColumnType(int tableIndex, int columnIndex)
    {
        return ((tableIndex + columnIndex) % 8) switch
        {
            0 => "int",
            1 => "bigint",
            2 => "decimal(18, 4)",
            3 => "nvarchar(200)",
            4 => "datetime2(7)",
            5 => "bit",
            6 => "uniqueidentifier",
            _ => "varbinary(128)",
        };
    }

    private static string GetSchemaName(int schemaIndex)
    {
        return $"db{schemaIndex:000}";
    }

    private static string GetTableName(int tableIndex)
    {
        return $"table_{tableIndex:0000}";
    }
}
