# Irion DuckDB.NET Integration Tests

This project contains Docker-backed integration tests for DuckDB extensions.
Docker must be running before executing this project.

Run the tests with:

```powershell
dotnet test .\Irion.DuckDB.NET.Test\Irion.DuckDB.NET.Test.csproj
```

## Container Framework

Use `DockerIntegrationFixture` and register only the containers a test class needs:

```csharp
public sealed class MyFixture : DockerIntegrationFixture
{
    protected override void Configure(DockerTestEnvironmentBuilder builder)
    {
        builder
            .AddPostgreSql()
            .AddMinio()
            .AddSqlServerExpress();
    }
}
```

Customize a known container by returning the modified Testcontainers builder:

```csharp
builder.AddPostgreSql(configure: pg => pg
    .WithImage("postgres:17.5-alpine")
    .WithDatabase("analytics"));
```

Register a fully custom container, including bind mounts or copied resources:

```csharp
builder.AddContainer("custom", "my-image:local", container => container
    .WithBindMount(@"C:\data", "/data"));
```

The framework starts containers before the fixture runs and disposes them in reverse order.

The SQL Server fixture prepares one SQL Server Express container with:

- 100 tables in `dbo`, each with 10 to 50 deterministic pseudo-random columns.
- 100 schemas named `db001` to `db100`.
- Full table copies from `dbo` into the schemas used by the MSSQL tests.

The MSSQL attach/close reproduction test runs 100 iterations by default. For local smoke runs:

```powershell
$env:IRION_DUCKDB_MSSQL_ATTACH_CLOSE_ITERATIONS = "10"
dotnet test .\Irion.DuckDB.NET.Test\Irion.DuckDB.NET.Test.csproj --filter DuckDb_can_open_attach_query_and_close_mssql_repeatedly
```

The MSSQL suite also includes:

- Reading representative SQL Server scalar types through DuckDB's `mssql` extension.
- Creating one SQL Server table per supported DuckDB scalar type through DuckDB's `mssql` extension, inserting one value per table, and reading those values back through DuckDB.
- Installing the `mssql` extension from the `Irion.DuckDb.Extensions.mssql` NuGet package by default.
- Supporting `IRION_DUCKDB_MSSQL_EXTENSION_REPOSITORY` only as an override for custom extension repositories.
- Opening DuckDB with `allow_unsigned_extensions=true` for the MSSQL extension, then running `SET allow_unsigned_extensions = false` immediately after `LOAD mssql`.
- Using an isolated local DuckDB `extension_directory` under the temp folder for installed extension files.

Override the MSSQL extension repository with:

```powershell
$env:IRION_DUCKDB_MSSQL_EXTENSION_REPOSITORY = "C:\duckdb-extensions"
```

The MinIO/S3 Parquet suite covers DuckDB's `httpfs` extension with a local S3-compatible service:

- Creating a MinIO bucket during fixture setup.
- Writing Parquet files to `s3://...` from DuckDB.
- Reading the same Parquet files back through DuckDB.NET.
- Reading multiple Parquet files with a glob path.
- Verifying representative scalar types and NULL values after a Parquet round trip.
- Reading Hive-partitioned Parquet datasets from S3.
- Reading files with schema evolution through `union_by_name`.
- Reading Zstandard-compressed Parquet files.
- Filtering and projecting a larger remote Parquet dataset.

Additional S3/Parquet integration cases worth adding next:

- Additional compressed Parquet variants such as Snappy.
- Failure diagnostics for wrong credentials, missing buckets, and missing objects.
- Extension setup through `CREATE SECRET` once the minimum DuckDB version supports the desired syntax consistently.

The DuckLake suite covers DuckDB's `ducklake` extension using DuckDB-managed secrets:

- PostgreSQL metadata catalog in a dedicated database using the `public` schema, with local filesystem Parquet data files.
- PostgreSQL metadata catalog in a dedicated database using the `public` schema, with MinIO/S3 Parquet data files.
- DuckLake `TYPE ducklake` secrets that reference a named PostgreSQL `TYPE postgres` secret through `METADATA_PARAMETERS`.
- MinIO/S3 access through a scoped `TYPE s3` secret.
- Snapshot metadata checks through `ducklake_snapshots`.
- Parquet file materialization checks through `ducklake_table_info`.

## Containerized Test Run

Run the integration test project from a .NET 10 SDK container:

```powershell
.\scripts\run-irion-integration-tests-container.ps1 -MsSqlAttachCloseIterations 10
```

The script mounts the Docker socket so Testcontainers inside the .NET SDK container can start PostgreSQL and SQL Server sibling containers.
It also sets `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` so the test container can connect to the mapped ports of those sibling containers.

## DuckDB Query Style

Keep DuckDB SQL visible in the tests and use `DuckDbTestQuery` only to manage open/execute/close:

```csharp
var result = await DuckDbTestQuery.ExecuteQueryAsync<string>(
    setup:
    """
    INSTALL postgres;
    LOAD postgres;
    ATTACH 'host=127.0.0.1 port=5432 dbname=duckdb user=duckdb password=duckdb' AS pg (TYPE POSTGRES);
    """,
    query:
    """
    SELECT name
    FROM pg.public.integration_numbers
    WHERE id = 1
    """);
```
