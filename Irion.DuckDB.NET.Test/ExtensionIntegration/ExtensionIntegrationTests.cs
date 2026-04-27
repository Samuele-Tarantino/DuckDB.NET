using Irion.DuckDB.NET.Test.DuckLake;
using Irion.DuckDB.NET.Test.MsSql;

namespace Irion.DuckDB.NET.Test.ExtensionIntegration;

public sealed class ExtensionIntegrationTests(ExtensionIntegrationFixture fixture)
    : IClassFixture<ExtensionIntegrationFixture>
{
    private const string DuckLakeAlias = "ducklake_integrated";
    private const string MsSqlAlias = "mssql_integrated";
    private const int ExpectedValidationCount = 5;

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("DuckDBExtension", "mssql")]
    [Trait("DuckLakeStorage", "FileSystem")]
    [Trait("FileFormat", "Parquet")]
    public Task DuckLake_filesystem_and_mssql_can_round_trip_and_join_across_extensions()
    {
        return ExecuteRoundTripAndJoinTestAsync(DuckLakeStorageKind.FileSystem);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("Service", "Minio")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("DuckDBExtension", "mssql")]
    [Trait("DuckLakeStorage", "S3")]
    [Trait("FileFormat", "Parquet")]
    public Task DuckLake_s3_and_mssql_can_round_trip_and_join_across_extensions()
    {
        return ExecuteRoundTripAndJoinTestAsync(DuckLakeStorageKind.S3);
    }

    private async Task ExecuteRoundTripAndJoinTestAsync(DuckLakeStorageKind storageKind)
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var metadataDatabase = $"extension_integration_ducklake_mssql_{storageKind.ToString().ToLowerInvariant()}_{suffix}";
        var dataPath = CreateDataPath(storageKind, suffix, out var localDataDirectory);
        var ordersFromDuckLakeTable = $"orders_from_ducklake_{suffix}";
        var customersSourceTable = $"customers_source_{suffix}";

        await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
        var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };
        var sqlServerConnectionString = SqlLiterals.DuckDbString(
            fixture.SqlServer.GetDuckDbConnectionString(ExtensionIntegrationFixture.SqlServerDatabaseName));

        try
        {
            var validationCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
                setup: CreateSetupSql(
                    storageKind,
                    postgres,
                    dataPath,
                    sqlServerConnectionString,
                    ordersFromDuckLakeTable,
                    customersSourceTable),
                query: CreateValidationSql(ordersFromDuckLakeTable, customersSourceTable),
                connectionStringOptions: MsSqlDuckDbExtension.ConnectionStringOptions());

            validationCount.Should().Be(ExpectedValidationCount);
        }
        finally
        {
            if (localDataDirectory is not null)
            {
                TryDeleteDirectory(localDataDirectory);
            }
        }
    }

    private string CreateSetupSql(
        DuckLakeStorageKind storageKind,
        DuckLakePostgresConnectionOptions postgres,
        string dataPath,
        string sqlServerConnectionString,
        string ordersFromDuckLakeTable,
        string customersSourceTable)
    {
        var s3SecretSql = storageKind == DuckLakeStorageKind.S3
            ? DuckLakeSql.CreateS3Secret("ducklake_s3_secret", fixture.Minio, dataPath)
            : string.Empty;

        return
            $$"""
            INSTALL postgres;
            LOAD postgres;
            INSTALL httpfs;
            LOAD httpfs;
            INSTALL ducklake;
            LOAD ducklake;

            {{MsSqlDuckDbExtension.InstallLoadAndSecureSql()}}

            {{DuckLakeSql.CreatePostgresMetadataSecret("ducklake_pg_secret", postgres)}}
            {{s3SecretSql}}
            {{DuckLakeSql.CreateDuckLakeSecret("ducklake_secret", "ducklake_pg_secret", dataPath)}}

            ATTACH 'ducklake:ducklake_secret' AS {{DuckLakeAlias}} (DATA_INLINING_ROW_LIMIT 0);
            ATTACH {{sqlServerConnectionString}} AS {{MsSqlAlias}} (TYPE MSSQL);

            USE {{DuckLakeAlias}};

            CREATE TABLE lake_orders
            (
                order_id INTEGER,
                customer_id INTEGER,
                amount DECIMAL(18, 2),
                source_label VARCHAR
            );

            INSERT INTO lake_orders VALUES
                (1, 101, 25.50, 'ducklake'),
                (2, 102, 40.00, 'ducklake'),
                (3, 101, 74.50, 'ducklake'),
                (4, 103, 120.00, 'ducklake'),
                (5, 104, 15.00, 'ducklake'),
                (6, 105, 90.00, 'ducklake');

            DROP TABLE IF EXISTS {{MsSqlAlias}}.dbo.{{customersSourceTable}};
            DROP TABLE IF EXISTS {{MsSqlAlias}}.dbo.{{ordersFromDuckLakeTable}};

            CREATE TABLE {{MsSqlAlias}}.dbo.{{customersSourceTable}}
            (
                customer_id INTEGER,
                customer_name VARCHAR,
                region VARCHAR,
                loyalty_points INTEGER
            );

            INSERT INTO {{MsSqlAlias}}.dbo.{{customersSourceTable}} VALUES
                (101, 'alice', 'north', 12),
                (102, 'bob', 'south', 4),
                (103, 'carla', 'north', 20),
                (104, 'dan', 'west', 6);

            CREATE TABLE {{MsSqlAlias}}.dbo.{{ordersFromDuckLakeTable}} AS
            SELECT order_id, customer_id, amount, source_label
            FROM lake_orders;

            CREATE TABLE customers_from_mssql AS
            SELECT customer_id, customer_name, region, loyalty_points
            FROM {{MsSqlAlias}}.dbo.{{customersSourceTable}};
            """;
    }

    private static string CreateValidationSql(string ordersFromDuckLakeTable, string customersSourceTable)
    {
        return
            $$"""
            WITH checks(name, passed) AS
            (
                SELECT
                    'ducklake_to_mssql',
                    (
                        SELECT COUNT(*) = 6
                           AND COALESCE(SUM(amount), 0)::DECIMAL(18, 2) = 365.00
                        FROM {{MsSqlAlias}}.dbo.{{ordersFromDuckLakeTable}}
                    )
                UNION ALL
                SELECT
                    'mssql_to_ducklake',
                    (
                        SELECT COUNT(*) = 4
                           AND COALESCE(SUM(loyalty_points), 0) = 42
                        FROM {{DuckLakeAlias}}.customers_from_mssql
                    )
                UNION ALL
                SELECT
                    'ducklake_orders_join_mssql_customers',
                    (
                        SELECT COUNT(*) = 3
                           AND COALESCE(SUM(o.amount), 0)::DECIMAL(18, 2) = 220.00
                        FROM {{DuckLakeAlias}}.lake_orders o
                        JOIN {{MsSqlAlias}}.dbo.{{customersSourceTable}} c
                          ON c.customer_id = o.customer_id
                        WHERE c.region = 'north'
                    )
                UNION ALL
                SELECT
                    'mssql_orders_join_ducklake_customers',
                    (
                        SELECT COUNT(*) = 2
                           AND COALESCE(SUM(o.amount), 0)::DECIMAL(18, 2) = 55.00
                        FROM {{MsSqlAlias}}.dbo.{{ordersFromDuckLakeTable}} o
                        JOIN {{DuckLakeAlias}}.customers_from_mssql c
                          ON c.customer_id = o.customer_id
                        WHERE c.loyalty_points < 10
                    )
                UNION ALL
                SELECT
                    'ducklake_parquet_files_materialized',
                    (
                        SELECT COUNT(*) > 0
                        FROM ducklake_table_info('{{DuckLakeAlias}}')
                        WHERE file_count > 0
                    )
            )
            SELECT COUNT(*)::INTEGER
            FROM checks
            WHERE passed
            """;
    }

    private static string CreateDataPath(
        DuckLakeStorageKind storageKind,
        string suffix,
        out string? localDataDirectory)
    {
        if (storageKind == DuckLakeStorageKind.S3)
        {
            localDataDirectory = null;
            return ExtensionIntegrationFixture.CreateObjectUri($"lake/{suffix}/");
        }

        localDataDirectory = Path.Combine(Path.GetTempPath(), $"extension-integration-ducklake-mssql-fs-{suffix}");
        Directory.CreateDirectory(localDataDirectory);
        return DuckLakeSql.NormalizeFileDataPath(localDataDirectory);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temporary DuckLake Parquet files.
        }
    }

    private enum DuckLakeStorageKind
    {
        FileSystem,
        S3,
    }
}
