namespace Irion.DuckDB.NET.Test.Infrastructure;

public static class DuckDbTestQuery
{
    public static async Task ExecuteAsync(
        string query,
        string? connectionStringOptions = null,
        CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(
            async connection => await ExecuteNonQueryAsync(connection, query, cancellationToken),
            connectionStringOptions,
            cancellationToken);
    }

    public static async Task<T> ExecuteQueryAsync<T>(
        string query,
        string? setup = null,
        string? connectionStringOptions = null,
        CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(
            async connection =>
            {
                if (!string.IsNullOrWhiteSpace(setup))
                {
                    await ExecuteNonQueryAsync(connection, setup, cancellationToken);
                }

                await using var command = connection.CreateCommand();
                command.CommandText = query;

                var value = await command.ExecuteScalarAsync(cancellationToken);
                return (T)Convert.ChangeType(value!, typeof(T));
            },
            connectionStringOptions,
            cancellationToken);
    }

    private static async Task<T> WithConnectionAsync<T>(
        Func<DuckDBConnection, Task<T>> execute,
        string? connectionStringOptions,
        CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"duckdb-net-test-{Guid.NewGuid():N}.db");

        try
        {
            await using var connection = await OpenAsync(databasePath, connectionStringOptions, cancellationToken);
            return await execute(connection);
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete(databasePath + ".wal");
        }
    }

    private static async Task<DuckDBConnection> OpenAsync(
        string databasePath,
        string? connectionStringOptions,
        CancellationToken cancellationToken)
    {
        var connectionString = $"DataSource={databasePath}";

        if (!string.IsNullOrWhiteSpace(connectionStringOptions))
        {
            connectionString += $";{connectionStringOptions}";
        }

        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<int> ExecuteNonQueryAsync(
        DuckDBConnection connection,
        string query,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void TryDelete(string path)
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
}
