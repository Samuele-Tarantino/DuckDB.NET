using Irion.DuckDB.NET.Test.S3;

namespace Irion.DuckDB.NET.Test.DuckLake;

public sealed class DuckLakePostgresS3ParquetTests(DuckLakePostgresS3Fixture fixture)
    : IClassFixture<DuckLakePostgresS3Fixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckLake_can_create_insert_and_read_table_with_postgres_metadata_and_s3_parquet()
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var metadataDatabase = $"ducklake_s3_{suffix}";
        var dataPath = DuckLakePostgresS3Fixture.CreateObjectUri($"lake/{suffix}/");
        await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
        var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };

        var rowCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            INSTALL postgres;
            LOAD postgres;
            INSTALL httpfs;
            LOAD httpfs;
            INSTALL ducklake;
            LOAD ducklake;

            {DuckLakeSql.CreatePostgresMetadataSecret("ducklake_pg_secret", postgres)}
            {DuckLakeSql.CreateS3Secret("ducklake_s3_secret", fixture.Minio, dataPath)}
            {DuckLakeSql.CreateDuckLakeSecret("ducklake_secret", "ducklake_pg_secret", dataPath)}

            ATTACH 'ducklake:ducklake_secret' AS ducklake_s3 (DATA_INLINING_ROW_LIMIT 0);
            USE ducklake_s3;

            CREATE TABLE metrics AS
            SELECT
                range::INTEGER AS id,
                CASE range % 3
                    WHEN 0 THEN 'cpu'
                    WHEN 1 THEN 'memory'
                    ELSE 'disk'
                END AS metric_name,
                (range * 1.25)::DOUBLE AS metric_value
            FROM range(1, 128);
            """,
            query:
            """
            SELECT COUNT(*)
            FROM ducklake_s3.metrics
            WHERE id = 50
              AND metric_name = 'disk'
              AND metric_value = 62.5
              AND (
                  SELECT COUNT(*)
                  FROM ducklake_table_info('ducklake_s3')
                  WHERE file_count > 0
              ) > 0
            """);

        rowCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "ducklake")]
    [Trait("DuckDBExtension", "postgres")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckLake_can_filter_project_and_append_with_postgres_metadata_and_s3_parquet()
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var metadataDatabase = $"ducklake_s3_{suffix}";
        var dataPath = DuckLakePostgresS3Fixture.CreateObjectUri($"lake/{suffix}/");
        await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
        var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };

        var total = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            INSTALL postgres;
            LOAD postgres;
            INSTALL httpfs;
            LOAD httpfs;
            INSTALL ducklake;
            LOAD ducklake;

            {DuckLakeSql.CreatePostgresMetadataSecret("ducklake_pg_secret", postgres)}
            {DuckLakeSql.CreateS3Secret("ducklake_s3_secret", fixture.Minio, dataPath)}
            {DuckLakeSql.CreateDuckLakeSecret("ducklake_secret", "ducklake_pg_secret", dataPath)}

            ATTACH 'ducklake:ducklake_secret' AS ducklake_s3 (DATA_INLINING_ROW_LIMIT 0);
            USE ducklake_s3;

            CREATE TABLE events AS
            SELECT
                range::INTEGER AS id,
                (range % 5)::INTEGER AS bucket,
                CASE WHEN range % 2 = 0 THEN 'even' ELSE 'odd' END AS parity,
                repeat('payload-', 10) || range::VARCHAR AS payload
            FROM range(0, 100);

            INSERT INTO events
            SELECT
                range::INTEGER AS id,
                (range % 5)::INTEGER AS bucket,
                CASE WHEN range % 2 = 0 THEN 'even' ELSE 'odd' END AS parity,
                repeat('payload-', 10) || range::VARCHAR AS payload
            FROM range(100, 110);
            """,
            query:
            """
            SELECT SUM(id)::INTEGER
            FROM
            (
                SELECT id, bucket
                FROM ducklake_s3.events
                WHERE parity = 'even'
            ) projected_events
            WHERE bucket = 4
            """);

        total.Should().Be(594);
    }
}
