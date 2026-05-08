using System.Data.Common;

namespace Irion.DuckDB.NET.Test.DuckLake;

public static class DuckLakePostgresContainerExtensions
{
    public static Task CreateDuckLakeDatabaseAsync(this PostgreSqlContainer container, string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        return container.ExecScriptAsync($"CREATE DATABASE {QuoteIdentifier(databaseName)};");
    }

    public static DuckLakePostgresConnectionOptions GetDuckLakeConnectionOptions(this PostgreSqlContainer container)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = container.GetConnectionString(),
        };

        return new DuckLakePostgresConnectionOptions(
            GetRequiredValue("Host", "Server"),
            int.Parse(GetRequiredValue("Port")),
            GetRequiredValue("Database"),
            GetRequiredValue("Username", "User ID", "UserId", "User"),
            GetRequiredValue("Password"));

        string GetRequiredValue(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
                {
                    return Convert.ToString(value)!;
                }
            }

            throw new InvalidOperationException($"The PostgreSQL connection string does not contain any of: {string.Join(", ", keys)}.");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}

public sealed record DuckLakePostgresConnectionOptions(
    string Host,
    int Port,
    string Database,
    string User,
    string Password);
