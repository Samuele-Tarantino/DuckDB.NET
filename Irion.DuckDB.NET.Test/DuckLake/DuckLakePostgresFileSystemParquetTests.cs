namespace Irion.DuckDB.NET.Test.DuckLake;

public sealed class DuckLakePostgresFileSystemParquetTests(DuckLakePostgresFileSystemFixture fixture)
    : IClassFixture<DuckLakePostgresFileSystemFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckLake_can_create_insert_and_read_table_with_postgres_metadata_and_filesystem_parquet()
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var metadataDatabase = $"ducklake_fs_{suffix}";
        var dataPath = DuckLakeSql.NormalizeFileDataPath(Path.Combine(Path.GetTempPath(), $"ducklake-fs-{suffix}"));
        await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
        var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };

        Directory.CreateDirectory(dataPath);

        try
        {
            var rowCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
                setup:
                $"""
                INSTALL postgres;
                LOAD postgres;
                INSTALL ducklake;
                LOAD ducklake;

                {DuckLakeSql.CreatePostgresMetadataSecret("ducklake_pg_secret", postgres)}
                {DuckLakeSql.CreateDuckLakeSecret("ducklake_secret", "ducklake_pg_secret", dataPath)}

                ATTACH 'ducklake:ducklake_secret' AS ducklake_fs (DATA_INLINING_ROW_LIMIT 0);
                USE ducklake_fs;

                CREATE TABLE orders
                (
                    id INTEGER,
                    customer VARCHAR,
                    amount DECIMAL(18, 2),
                    created_on DATE
                );

                INSERT INTO orders
                SELECT
                    range::INTEGER AS id,
                    'customer-' || range::VARCHAR AS customer,
                    (range * 10.25)::DECIMAL(18, 2) AS amount,
                    DATE '2026-04-24' + (range::INTEGER % 7) AS created_on
                FROM range(1, 128);
                """,
                query:
                """
                SELECT COUNT(*)
                FROM ducklake_fs.orders
                WHERE id = 42
                  AND customer = 'customer-42'
                  AND amount = 430.50
                  AND created_on = DATE '2026-04-24'
                  AND (
                      SELECT COUNT(*)
                      FROM ducklake_table_info('ducklake_fs')
                      WHERE file_count > 0
                  ) > 0
                """);

            rowCount.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(dataPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckLake_can_query_snapshot_metadata_with_postgres_metadata_and_filesystem_parquet()
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var metadataDatabase = $"ducklake_fs_{suffix}";
        var dataPath = DuckLakeSql.NormalizeFileDataPath(Path.Combine(Path.GetTempPath(), $"ducklake-fs-{suffix}"));
        await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
        var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };

        Directory.CreateDirectory(dataPath);

        try
        {
            var snapshotCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
                setup:
                $"""
                INSTALL postgres;
                LOAD postgres;
                INSTALL ducklake;
                LOAD ducklake;

                {DuckLakeSql.CreatePostgresMetadataSecret("ducklake_pg_secret", postgres)}
                {DuckLakeSql.CreateDuckLakeSecret("ducklake_secret", "ducklake_pg_secret", dataPath)}

                ATTACH 'ducklake:ducklake_secret' AS ducklake_fs (DATA_INLINING_ROW_LIMIT 0);
                USE ducklake_fs;

                CREATE TABLE events AS
                SELECT range::INTEGER AS id, 'created' AS state
                FROM range(0, 5);

                INSERT INTO events VALUES (5, 'appended');
                """,
                query:
                """
                SELECT COUNT(*)::INTEGER
                FROM ducklake_snapshots('ducklake_fs')
                """);

            snapshotCount.Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            TryDeleteDirectory(dataPath);
        }
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
}
