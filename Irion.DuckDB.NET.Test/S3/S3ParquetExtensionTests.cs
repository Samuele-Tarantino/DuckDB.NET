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
}
