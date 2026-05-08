using Irion.DuckDB.NET.Test.DuckLake;
using Irion.DuckDB.NET.Test.MsSql;
using Irion.DuckDB.NET.Test.PostgreSql;

namespace Irion.DuckDB.NET.Test.ExtensionIntegration;

public sealed class ExtensionIntegrationTests(ExtensionIntegrationFixture fixture)
    : IClassFixture<ExtensionIntegrationFixture>
{
    private const string DuckLakeFileSystemAlias = "ducklake_fs_integrated";
    private const string DuckLakeS3Alias = "ducklake_s3_integrated";
    private const string MsSqlAlias = "mssql_integrated";
    private const string PostgresAlias = "postgres_integrated";

    // This suite is only for DuckDB extensions that expose a tabular data store, or connectors to one,
    // where DuckDB can create/insert data and read it back with SQL. Utility-only extensions do not
    // belong here because they cannot participate as a source or target in cross-store copy/join tests.
    // Add a new data-store extension here and in CreateStore/CreateExtensionSetupSql to include it in
    // every 2..N combination.
    private static readonly StoreKind[] DataStoreExtensions =
    [
        StoreKind.DuckLakeFileSystem,
        StoreKind.DuckLakeS3,
        StoreKind.Postgres,
        StoreKind.MsSql,
        StoreKind.HttpFs,
    ];

    [Theory]
    [MemberData(nameof(ExtensionCombinations))]
    [Trait("Category", "Integration")]
    [Trait("Service", "PostgreSql")]
    [Trait("Service", "Minio")]
    [Trait("Service", "SqlServer")]
    [Trait("DuckDBExtension", "extension-combination")]
    [Trait("FileFormat", "Parquet")]
    public async Task Extension_combination_can_create_insert_copy_and_join(
        string combinationName,
        StoreKind[] storeKinds)
    {
        var suffix = DuckLakeSql.CreateIdentifierSuffix();
        var tables = ExtensionTableNames.Create(suffix);
        var stores = storeKinds.Select(CreateStore).ToArray();
        var duckLakeStorages = await CreateDuckLakeStoragesAsync(stores, suffix);

        try
        {
            await ExecuteSetupAndAssertAsync(
                setup: string.Concat(
                    CreateExtensionSetupSql(stores, duckLakeStorages),
                    CreateSeedTablesSql(stores, tables),
                    CreateAllOrderedCopiesSql(stores, tables)),
                assertAsync: async connection =>
                {
                    await AssertAllOrderedCopiesAsync(connection, stores, tables, combinationName);
                    await AssertAllOrderedPairJoinsAsync(connection, stores, tables, combinationName);
                    await AssertCombinationJoinAsync(connection, stores, tables, combinationName);
                    await AssertDuckLakeFilesMaterializedIfNeededAsync(connection, stores, combinationName);
                },
                connectionStringOptions: CreateConnectionStringOptions(stores));
        }
        finally
        {
            foreach (var storage in duckLakeStorages.Values)
            {
                TryDeleteDirectory(storage.LocalDataDirectory);
            }
        }
    }

    public static TheoryData<string, StoreKind[]> ExtensionCombinations()
    {
        var data = new TheoryData<string, StoreKind[]>();

        foreach (var combination in CreateCombinations(DataStoreExtensions, minimumSize: 2))
        {
            data.Add(CreateCombinationName(combination), combination);
        }

        return data;
    }

    private async Task<Dictionary<StoreKind, DuckLakeStorage>> CreateDuckLakeStoragesAsync(
        IReadOnlyCollection<ExtensionStore> stores,
        string suffix)
    {
        var storages = new Dictionary<StoreKind, DuckLakeStorage>();

        foreach (var store in stores.Where(store => store.IsDuckLake))
        {
            var metadataDatabase = $"extension_integration_{store.Name}_{suffix}";
            var dataPath = CreateDuckLakeDataPath(store, suffix, out var localDataDirectory);

            await fixture.PostgreSql.CreateDuckLakeDatabaseAsync(metadataDatabase);
            var postgres = fixture.PostgreSql.GetDuckLakeConnectionOptions() with { Database = metadataDatabase };

            storages.Add(store.Kind, new DuckLakeStorage(store.Kind, postgres, dataPath, localDataDirectory));
        }

        return storages;
    }

    private string CreateExtensionSetupSql(
        IReadOnlyCollection<ExtensionStore> stores,
        IReadOnlyDictionary<StoreKind, DuckLakeStorage> duckLakeStorages)
    {
        var setup = new StringBuilder();

        AppendExtensionLoadSql(setup, stores);
        AppendDuckLakeSecretSql(setup, stores, duckLakeStorages);
        AppendHttpFsSecretSql(setup, stores);
        AppendMsSqlLoadSql(setup, stores);
        AppendAttachSql(setup, stores);

        return setup.ToString();
    }

    private static void AppendExtensionLoadSql(StringBuilder setup, IReadOnlyCollection<ExtensionStore> stores)
    {
        if (RequiresPostgresExtension(stores))
        {
            setup.AppendLine(
                """
                INSTALL postgres;
                LOAD postgres;
                """);
        }

        if (RequiresHttpFsExtension(stores))
        {
            setup.AppendLine(
                """
                INSTALL httpfs;
                LOAD httpfs;
                """);
        }

        if (stores.Any(store => store.IsDuckLake))
        {
            setup.AppendLine(
                """
                INSTALL ducklake;
                LOAD ducklake;
                """);
        }
    }

    private void AppendDuckLakeSecretSql(
        StringBuilder setup,
        IEnumerable<ExtensionStore> stores,
        IReadOnlyDictionary<StoreKind, DuckLakeStorage> duckLakeStorages)
    {
        foreach (var store in stores.Where(store => store.IsDuckLake))
        {
            var storage = duckLakeStorages[store.Kind];
            var postgresSecretName = $"{store.Name}_pg_secret";
            var duckLakeSecretName = $"{store.Name}_ducklake_secret";

            setup.AppendLine(DuckLakeSql.CreatePostgresMetadataSecret(postgresSecretName, storage.Postgres));

            if (store.Kind == StoreKind.DuckLakeS3)
            {
                setup.AppendLine(DuckLakeSql.CreateS3Secret($"{store.Name}_s3_secret", fixture.Minio, storage.DataPath));
            }

            setup.AppendLine(DuckLakeSql.CreateDuckLakeSecret(duckLakeSecretName, postgresSecretName, storage.DataPath));
        }
    }

    private void AppendHttpFsSecretSql(StringBuilder setup, IEnumerable<ExtensionStore> stores)
    {
        if (!stores.Any(store => store.Kind == StoreKind.HttpFs))
        {
            return;
        }

        setup.AppendLine(DuckLakeSql.CreateS3Secret(
            "httpfs_s3_secret",
            fixture.Minio,
            ExtensionIntegrationFixture.CreateObjectUri(string.Empty)));
    }

    private static void AppendMsSqlLoadSql(StringBuilder setup, IEnumerable<ExtensionStore> stores)
    {
        if (stores.Any(store => store.Kind == StoreKind.MsSql))
        {
            setup.AppendLine(MsSqlDuckDbExtension.InstallLoadAndSecureSql());
        }
    }

    private void AppendAttachSql(StringBuilder setup, IEnumerable<ExtensionStore> stores)
    {
        foreach (var store in stores)
        {
            switch (store.Kind)
            {
                case StoreKind.DuckLakeFileSystem:
                case StoreKind.DuckLakeS3:
                    setup.AppendLine($"ATTACH 'ducklake:{store.Name}_ducklake_secret' AS {store.Prefix} (DATA_INLINING_ROW_LIMIT 0);");
                    break;

                case StoreKind.Postgres:
                    var postgresConnectionString = SqlLiterals.DuckDbString(fixture.PostgreSql.GetDuckDbConnectionString());
                    setup.AppendLine($"ATTACH {postgresConnectionString} AS {PostgresAlias} (TYPE POSTGRES);");
                    break;

                case StoreKind.MsSql:
                    var sqlServerConnectionString = SqlLiterals.DuckDbString(
                        fixture.SqlServer.GetDuckDbConnectionString(ExtensionIntegrationFixture.SqlServerDatabaseName));
                    setup.AppendLine($"ATTACH {sqlServerConnectionString} AS {MsSqlAlias} (TYPE MSSQL);");
                    break;

                case StoreKind.HttpFs:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(stores), store.Kind, null);
            }
        }
    }

    private static string CreateSeedTablesSql(IEnumerable<ExtensionStore> stores, ExtensionTableNames tables)
    {
        return string.Concat(stores.Select(store =>
            CreateMaterializeTableSql(store, tables.Name(IntegrationTable.Customers), IntegrationTable.Customers, CreateSeedSelectSql(IntegrationTable.Customers, store))
            + CreateMaterializeTableSql(store, tables.Name(IntegrationTable.Orders), IntegrationTable.Orders, CreateSeedSelectSql(IntegrationTable.Orders, store))));
    }

    private static string CreateAllOrderedCopiesSql(
        IReadOnlyList<ExtensionStore> stores,
        ExtensionTableNames tables)
    {
        return string.Concat(OrderedPairs(stores).Select(pair =>
            CreateCopySql(pair.Source, pair.Target, tables, IntegrationTable.Customers)
            + CreateCopySql(pair.Source, pair.Target, tables, IntegrationTable.Orders)));
    }

    private static string CreateCopySql(
        ExtensionStore source,
        ExtensionStore target,
        ExtensionTableNames tables,
        IntegrationTable table)
    {
        return CreateMaterializeTableSql(
            target,
            tables.NameFrom(table, source),
            table,
            $"SELECT {Columns(table)} FROM {source.ReadExpression(tables.Name(table))}");
    }

    private static string CreateMaterializeTableSql(
        ExtensionStore target,
        string tableName,
        IntegrationTable table,
        string selectSql)
    {
        if (target.Kind == StoreKind.HttpFs)
        {
            return
                $$"""
                COPY (
                    {{selectSql}}
                ) TO {{SqlLiterals.DuckDbString(target.Uri(tableName))}} (FORMAT PARQUET);
                """;
        }

        return
            $$"""
            DROP TABLE IF EXISTS {{target.Qualified(tableName)}};
            CREATE TABLE {{target.Qualified(tableName)}}
            (
                {{ColumnDefinitions(table)}}
            );

            INSERT INTO {{target.Qualified(tableName)}}
            {{selectSql}};
            """;
    }

    private static string CreateSeedSelectSql(IntegrationTable table, ExtensionStore store)
    {
        return table switch
        {
            IntegrationTable.Customers =>
                $$"""
                SELECT customer_id, customer_name, region, loyalty_points, source_name
                FROM (
                    VALUES
                        (101, 'alice', 'north', 12, '{{store.Name}}'),
                        (102, 'bob', 'south', 4, '{{store.Name}}'),
                        (103, 'carla', 'north', 20, '{{store.Name}}'),
                        (104, 'dan', 'west', 6, '{{store.Name}}')
                ) AS customers(customer_id, customer_name, region, loyalty_points, source_name)
                """,

            IntegrationTable.Orders =>
                $$"""
                SELECT order_id, customer_id, amount, source_name
                FROM (
                    VALUES
                        (1, 101, 25.50, '{{store.Name}}'),
                        (2, 102, 40.00, '{{store.Name}}'),
                        (3, 101, 74.50, '{{store.Name}}'),
                        (4, 103, 120.00, '{{store.Name}}'),
                        (5, 104, 15.00, '{{store.Name}}'),
                        (6, 105, 90.00, '{{store.Name}}')
                ) AS orders(order_id, customer_id, amount, source_name)
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
        };
    }

    private static async Task AssertAllOrderedCopiesAsync(
        DuckDBConnection connection,
        IReadOnlyList<ExtensionStore> stores,
        ExtensionTableNames tables,
        string combinationName)
    {
        foreach (var (source, target) in OrderedPairs(stores))
        {
            await AssertCopiedTableAsync(connection, source, target, tables, IntegrationTable.Customers, expectedRows: 4, combinationName);
            await AssertCopiedTableAsync(connection, source, target, tables, IntegrationTable.Orders, expectedRows: 6, combinationName);
        }
    }

    private static async Task AssertCopiedTableAsync(
        DuckDBConnection connection,
        ExtensionStore source,
        ExtensionStore target,
        ExtensionTableNames tables,
        IntegrationTable table,
        int expectedRows,
        string combinationName)
    {
        var copiedRows = await ExecuteScalarAsync<int>(
            connection,
            $"SELECT COUNT(*) FROM {target.ReadExpression(tables.NameFrom(table, source))} WHERE source_name = '{source.Name}'");
        copiedRows.Should().Be(expectedRows, $"{combinationName}: {source.Name} {table.ToString().ToLowerInvariant()} should be inserted into {target.Name}");

        if (table != IntegrationTable.Orders)
        {
            return;
        }

        var copiedOrderAmount = await ExecuteScalarAsync<decimal>(
            connection,
            $"SELECT COALESCE(SUM(amount), 0)::DECIMAL(18, 2) FROM {target.ReadExpression(tables.NameFrom(table, source))} WHERE source_name = '{source.Name}'");
        copiedOrderAmount.Should().Be(365.00m, $"{combinationName}: {source.Name} order amounts should survive the write into {target.Name}");
    }

    private static async Task AssertAllOrderedPairJoinsAsync(
        DuckDBConnection connection,
        IReadOnlyList<ExtensionStore> stores,
        ExtensionTableNames tables,
        string combinationName)
    {
        foreach (var (orders, customers) in OrderedPairs(stores))
        {
            await AssertPairJoinAsync(connection, orders, customers, tables, combinationName);
        }
    }

    private static async Task AssertPairJoinAsync(
        DuckDBConnection connection,
        ExtensionStore orders,
        ExtensionStore customers,
        ExtensionTableNames tables,
        string combinationName)
    {
        var fromSql =
            $$"""
            FROM {{orders.ReadExpression(tables.Name(IntegrationTable.Orders))}} o
            JOIN {{customers.ReadExpression(tables.Name(IntegrationTable.Customers))}} c
              ON c.customer_id = o.customer_id
            """;

        var whereSql =
            $$"""
            WHERE c.region = 'north'
              AND o.source_name = '{{orders.Name}}'
              AND c.source_name = '{{customers.Name}}'
            """;

        await AssertJoinResultAsync(
            connection,
            fromSql,
            whereSql,
            $"{combinationName}: {orders.Name} orders should join with {customers.Name} customers");
    }

    private static async Task AssertCombinationJoinAsync(
        DuckDBConnection connection,
        IReadOnlyList<ExtensionStore> stores,
        ExtensionTableNames tables,
        string combinationName)
    {
        var orders = stores[0];
        var customers = stores.Skip(1).ToArray();

        await AssertJoinResultAsync(
            connection,
            CreateCombinationJoinFromSql(orders, customers, tables),
            CreateCombinationJoinWhereSql(orders, customers),
            $"{combinationName}: all stores should participate in one join");
    }

    private static async Task AssertJoinResultAsync(
        DuckDBConnection connection,
        string fromSql,
        string whereSql,
        string reason)
    {
        var joinedOrderCount = await ExecuteScalarAsync<int>(
            connection,
            $$"""
            SELECT COUNT(*)
            {{fromSql}}
            {{whereSql}}
            """);
        joinedOrderCount.Should().Be(3, reason);

        var joinedOrderAmount = await ExecuteScalarAsync<decimal>(
            connection,
            $$"""
            SELECT COALESCE(SUM(o.amount), 0)::DECIMAL(18, 2)
            {{fromSql}}
            {{whereSql}}
            """);
        joinedOrderAmount.Should().Be(220.00m, reason);
    }

    private static string CreateCombinationJoinFromSql(
        ExtensionStore orders,
        IReadOnlyList<ExtensionStore> customers,
        ExtensionTableNames tables)
    {
        var sql = new StringBuilder();
        sql.AppendLine($"FROM {orders.ReadExpression(tables.Name(IntegrationTable.Orders))} o");

        for (var index = 0; index < customers.Count; index++)
        {
            sql.AppendLine($"JOIN {customers[index].ReadExpression(tables.Name(IntegrationTable.Customers))} c{index}");
            sql.AppendLine($"  ON c{index}.customer_id = o.customer_id");
        }

        return sql.ToString();
    }

    private static string CreateCombinationJoinWhereSql(
        ExtensionStore orders,
        IReadOnlyList<ExtensionStore> customers)
    {
        var predicates = new List<string>
        {
            $"o.source_name = '{orders.Name}'",
            "c0.region = 'north'",
        };

        for (var index = 0; index < customers.Count; index++)
        {
            predicates.Add($"c{index}.source_name = '{customers[index].Name}'");
        }

        for (var index = 1; index < customers.Count; index++)
        {
            predicates.Add($"c{index}.region = c0.region");
        }

        return "WHERE " + string.Join($"{Environment.NewLine}  AND ", predicates);
    }

    private static async Task AssertDuckLakeFilesMaterializedIfNeededAsync(
        DuckDBConnection connection,
        IEnumerable<ExtensionStore> stores,
        string combinationName)
    {
        foreach (var store in stores.Where(store => store.IsDuckLake))
        {
            var materializedFileTables = await ExecuteScalarAsync<int>(
                connection,
                $"SELECT COUNT(*) FROM ducklake_table_info('{store.Prefix}') WHERE file_count > 0");
            materializedFileTables.Should().BeGreaterThan(0, $"{combinationName}: {store.Name} should materialize Parquet files");
        }
    }

    private static IEnumerable<StoreKind[]> CreateCombinations(StoreKind[] stores, int minimumSize)
    {
        for (var size = minimumSize; size <= stores.Length; size++)
        {
            foreach (var combination in CreateCombinations(stores, size, startIndex: 0))
            {
                yield return combination;
            }
        }
    }

    private static IEnumerable<StoreKind[]> CreateCombinations(StoreKind[] stores, int size, int startIndex)
    {
        if (size == 0)
        {
            yield return [];
            yield break;
        }

        for (var index = startIndex; index <= stores.Length - size; index++)
        {
            foreach (var tail in CreateCombinations(stores, size - 1, index + 1))
            {
                yield return new[] { stores[index] }.Concat(tail).ToArray();
            }
        }
    }

    private static string CreateCombinationName(IEnumerable<StoreKind> stores)
    {
        return string.Join("__", stores.Select(store => CreateStore(store).Name));
    }

    private static IEnumerable<(ExtensionStore Source, ExtensionStore Target)> OrderedPairs(IReadOnlyList<ExtensionStore> stores)
    {
        for (var sourceIndex = 0; sourceIndex < stores.Count; sourceIndex++)
        {
            for (var targetIndex = 0; targetIndex < stores.Count; targetIndex++)
            {
                if (sourceIndex != targetIndex)
                {
                    yield return (stores[sourceIndex], stores[targetIndex]);
                }
            }
        }
    }

    private static bool RequiresPostgresExtension(IEnumerable<ExtensionStore> stores)
    {
        return stores.Any(store => store.Kind == StoreKind.Postgres || store.IsDuckLake);
    }

    private static bool RequiresHttpFsExtension(IEnumerable<ExtensionStore> stores)
    {
        return stores.Any(store => store.Kind is StoreKind.HttpFs or StoreKind.DuckLakeS3);
    }

    private static ExtensionStore CreateStore(StoreKind kind)
    {
        return kind switch
        {
            StoreKind.DuckLakeFileSystem => new ExtensionStore(kind, "ducklake_fs", DuckLakeFileSystemAlias),
            StoreKind.DuckLakeS3 => new ExtensionStore(kind, "ducklake_s3", DuckLakeS3Alias),
            StoreKind.Postgres => new ExtensionStore(kind, "postgres", $"{PostgresAlias}.public"),
            StoreKind.MsSql => new ExtensionStore(kind, "mssql", $"{MsSqlAlias}.dbo"),
            StoreKind.HttpFs => new ExtensionStore(kind, "httpfs", Prefix: null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static string? CreateConnectionStringOptions(IEnumerable<ExtensionStore> stores)
    {
        return stores.Any(store => store.Kind == StoreKind.MsSql)
            ? MsSqlDuckDbExtension.ConnectionStringOptions()
            : null;
    }

    private static string CreateDuckLakeDataPath(
        ExtensionStore store,
        string suffix,
        out string? localDataDirectory)
    {
        if (store.Kind == StoreKind.DuckLakeS3)
        {
            localDataDirectory = null;
            return ExtensionIntegrationFixture.CreateObjectUri($"{store.Name}/{suffix}/");
        }

        localDataDirectory = Path.Combine(Path.GetTempPath(), $"extension-integration-{store.Name}-{suffix}");
        Directory.CreateDirectory(localDataDirectory);
        return DuckLakeSql.NormalizeFileDataPath(localDataDirectory);
    }

    private static string Columns(IntegrationTable table)
    {
        return table switch
        {
            IntegrationTable.Customers => "customer_id, customer_name, region, loyalty_points, source_name",
            IntegrationTable.Orders => "order_id, customer_id, amount, source_name",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
        };
    }

    private static string ColumnDefinitions(IntegrationTable table)
    {
        return table switch
        {
            IntegrationTable.Customers =>
                """
                customer_id INTEGER,
                customer_name VARCHAR,
                region VARCHAR,
                loyalty_points INTEGER,
                source_name VARCHAR
                """,

            IntegrationTable.Orders =>
                """
                order_id INTEGER,
                customer_id INTEGER,
                amount DECIMAL(18, 2),
                source_name VARCHAR
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
        };
    }

    private static async Task ExecuteSetupAndAssertAsync(
        string setup,
        Func<DuckDBConnection, Task> assertAsync,
        string? connectionStringOptions)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"duckdb-net-extension-integration-{Guid.NewGuid():N}.db");

        try
        {
            await using var connection = await OpenDuckDbConnectionAsync(databasePath, connectionStringOptions);
            await ExecuteNonQueryAsync(connection, setup);
            await assertAsync(connection);
        }
        finally
        {
            TryDeleteFile(databasePath);
            TryDeleteFile(databasePath + ".wal");
        }
    }

    private static async Task<DuckDBConnection> OpenDuckDbConnectionAsync(
        string databasePath,
        string? connectionStringOptions)
    {
        var connectionString = $"DataSource={databasePath}";

        if (!string.IsNullOrWhiteSpace(connectionStringOptions))
        {
            connectionString += $";{connectionStringOptions}";
        }

        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteNonQueryAsync(DuckDBConnection connection, string query)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(DuckDBConnection connection, string query)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private static void TryDeleteDirectory(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temporary DuckLake Parquet files.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temporary DuckDB files.
        }
    }

    public enum StoreKind
    {
        DuckLakeFileSystem,
        DuckLakeS3,
        Postgres,
        MsSql,
        HttpFs,
    }

    private enum IntegrationTable
    {
        Customers,
        Orders,
    }

    private sealed record ExtensionStore(StoreKind Kind, string Name, string? Prefix)
    {
        public bool IsDuckLake => Kind is StoreKind.DuckLakeFileSystem or StoreKind.DuckLakeS3;

        public string Qualified(string tableName)
        {
            if (Kind == StoreKind.HttpFs)
            {
                throw new InvalidOperationException("httpfs Parquet paths are not SQL tables.");
            }

            return $"{Prefix}.{tableName}";
        }

        public string ReadExpression(string tableName)
        {
            return Kind == StoreKind.HttpFs
                ? $"read_parquet({SqlLiterals.DuckDbString(Uri(tableName))})"
                : Qualified(tableName);
        }

        public string Uri(string tableName)
        {
            return ExtensionIntegrationFixture.CreateObjectUri($"httpfs/{tableName}.parquet");
        }
    }

    private sealed record ExtensionTableNames(string Customers, string Orders, string Suffix)
    {
        public static ExtensionTableNames Create(string suffix)
        {
            return new ExtensionTableNames(
                $"integration_customers_{suffix}",
                $"integration_orders_{suffix}",
                suffix);
        }

        public string Name(IntegrationTable table)
        {
            return table switch
            {
                IntegrationTable.Customers => Customers,
                IntegrationTable.Orders => Orders,
                _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
            };
        }

        public string NameFrom(IntegrationTable table, ExtensionStore source)
        {
            return table switch
            {
                IntegrationTable.Customers => $"integration_customers_from_{source.Name}_{Suffix}",
                IntegrationTable.Orders => $"integration_orders_from_{source.Name}_{Suffix}",
                _ => throw new ArgumentOutOfRangeException(nameof(table), table, null),
            };
        }
    }

    private sealed record DuckLakeStorage(
        StoreKind Kind,
        DuckLakePostgresConnectionOptions Postgres,
        string DataPath,
        string? LocalDataDirectory);
}
