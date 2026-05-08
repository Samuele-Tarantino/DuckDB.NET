namespace Irion.DuckDB.NET.Test.S3;

public sealed class S3ParquetExtensionTests(S3ParquetIntegrationFixture fixture) : IClassFixture<S3ParquetIntegrationFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_write_and_read_parquet_file_from_s3()
    {
        var parquetUri = SqlLiterals.DuckDbString(
            S3ParquetIntegrationFixture.CreateObjectUri($"roundtrip/{Guid.NewGuid():N}/numbers.parquet"));

        var rowCount = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT *
                FROM
                (
                    VALUES
                        (1, 'one', DATE '2026-04-24'),
                        (2, 'two', DATE '2026-04-25'),
                        (3, 'three', DATE '2026-04-26')
                ) values_under_test(id, name, created_on)
            )
            TO {parquetUri} (FORMAT PARQUET);
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM read_parquet({parquetUri})
            WHERE id = 2
              AND name = 'two'
              AND created_on = DATE '2026-04-25'
            """);

        rowCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_read_multiple_s3_parquet_files_with_glob()
    {
        var prefix = $"glob/{Guid.NewGuid():N}";
        var firstParquetUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/part-1.parquet"));
        var secondParquetUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/part-2.parquet"));
        var globUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/*.parquet"));

        var total = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY (SELECT 10 AS amount UNION ALL SELECT 20) TO {firstParquetUri} (FORMAT PARQUET);
            COPY (SELECT 30 AS amount UNION ALL SELECT 40) TO {secondParquetUri} (FORMAT PARQUET);
            """,
            query:
            $"""
            SELECT SUM(amount)::INTEGER
            FROM read_parquet({globUri})
            """);

        total.Should().Be(100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_preserves_common_scalar_types_and_nulls_in_s3_parquet()
    {
        var parquetUri = SqlLiterals.DuckDbString(
            S3ParquetIntegrationFixture.CreateObjectUri($"types/{Guid.NewGuid():N}/scalars.parquet"));

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT
                    1::INTEGER AS id,
                    true AS enabled,
                    12345.6789::DECIMAL(18, 4) AS amount,
                    TIMESTAMP '2026-04-24 12:34:56.123456' AS created_at,
                    NULL::VARCHAR AS optional_text
            )
            TO {parquetUri} (FORMAT PARQUET);
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM read_parquet({parquetUri})
            WHERE id = 1
              AND enabled = true
              AND amount = 12345.6789
              AND created_at = TIMESTAMP '2026-04-24 12:34:56.123456'
              AND optional_text IS NULL
            """);

        matchingRows.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_read_hive_partitioned_parquet_dataset_from_s3()
    {
        var prefix = $"partitioned/{Guid.NewGuid():N}";
        var datasetUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri(prefix));
        var globUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/*/*/*.parquet"));

        var total = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT *
                FROM
                (
                    VALUES
                        (1, DATE '2026-04-24', 'it', 10),
                        (2, DATE '2026-04-24', 'us', 20),
                        (3, DATE '2026-04-25', 'it', 30),
                        (4, DATE '2026-04-25', 'us', 40)
                ) values_under_test(id, business_date, region, amount)
            )
            TO {datasetUri} (FORMAT PARQUET, PARTITION_BY (business_date, region));
            """,
            query:
            $"""
            SELECT SUM(amount)::INTEGER
            FROM read_parquet({globUri}, hive_partitioning = true)
            WHERE business_date::DATE = DATE '2026-04-25'
              AND region = 'it'
            """);

        total.Should().Be(30);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_read_s3_parquet_files_with_schema_evolution_by_name()
    {
        var prefix = $"schema-evolution/{Guid.NewGuid():N}";
        var firstParquetUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/part-1.parquet"));
        var secondParquetUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/part-2.parquet"));
        var globUri = SqlLiterals.DuckDbString(S3ParquetIntegrationFixture.CreateObjectUri($"{prefix}/*.parquet"));

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT 1 AS id, 'legacy' AS label
            )
            TO {firstParquetUri} (FORMAT PARQUET);

            COPY
            (
                SELECT 2 AS id, 'current' AS label, 99 AS score
            )
            TO {secondParquetUri} (FORMAT PARQUET);
            """,
            query:
            $"""
            SELECT COUNT(*)
            FROM read_parquet({globUri}, union_by_name = true)
            WHERE (id = 1 AND label = 'legacy' AND score IS NULL)
               OR (id = 2 AND label = 'current' AND score = 99)
            """);

        matchingRows.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_read_zstd_compressed_parquet_file_from_s3()
    {
        var parquetUri = SqlLiterals.DuckDbString(
            S3ParquetIntegrationFixture.CreateObjectUri($"compression/{Guid.NewGuid():N}/zstd.parquet"));

        var matchingRows = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT range AS id, repeat('duckdb-net-', 20) || range::VARCHAR AS payload
                FROM range(0, 128)
            )
            TO {parquetUri} (FORMAT PARQUET, COMPRESSION ZSTD);
            """,
            query:
            $"""
            SELECT COUNT(*)::INTEGER
            FROM read_parquet({parquetUri})
            WHERE id BETWEEN 10 AND 19
              AND payload LIKE 'duckdb-net-%'
            """);

        matchingRows.Should().Be(10);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Service", "Minio")]
    [Trait("DuckDBExtension", "httpfs")]
    [Trait("FileFormat", "Parquet")]
    public async Task DuckDb_can_filter_and_project_larger_s3_parquet_dataset()
    {
        var parquetUri = SqlLiterals.DuckDbString(
            S3ParquetIntegrationFixture.CreateObjectUri($"query-shape/{Guid.NewGuid():N}/events.parquet"));

        var total = await DuckDbTestQuery.ExecuteQueryAsync<int>(
            setup:
            $"""
            {fixture.CreateDuckDbS3SetupSql()}

            COPY
            (
                SELECT
                    range::INTEGER AS id,
                    (range % 10)::INTEGER AS bucket,
                    CASE WHEN range % 2 = 0 THEN 'even' ELSE 'odd' END AS parity,
                    repeat('ignored-column-', 8) || range::VARCHAR AS payload
                FROM range(0, 1000)
            )
            TO {parquetUri} (FORMAT PARQUET);
            """,
            query:
            $"""
            SELECT SUM(id)::INTEGER
            FROM
            (
                SELECT id, bucket
                FROM read_parquet({parquetUri})
                WHERE parity = 'even'
            ) projected_events
            WHERE bucket = 4
            """);

        total.Should().Be(49_900);
    }
}
