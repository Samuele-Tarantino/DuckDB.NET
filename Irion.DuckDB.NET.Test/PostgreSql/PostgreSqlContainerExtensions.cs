using System.Data.Common;

namespace Irion.DuckDB.NET.Test.PostgreSql;

public static class PostgreSqlContainerExtensions
{
    public static string GetDuckDbConnectionString(this PostgreSqlContainer container)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = container.GetConnectionString(),
        };

        return string.Join(
            " ",
            new[]
            {
                ("host", GetValue("Host", "Server")),
                ("port", GetValue("Port")),
                ("dbname", GetValue("Database")),
                ("user", GetValue("Username", "User ID", "UserId", "User")),
                ("password", GetValue("Password")),
            }
            .Where(part => !string.IsNullOrWhiteSpace(part.Item2))
            .Select(part => $"{part.Item1}={LibPqValue(part.Item2!)}"));

        string? GetValue(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value))
                {
                    return Convert.ToString(value);
                }
            }

            return null;
        }
    }

    private static string LibPqValue(string value)
    {
        if (value.All(c => !char.IsWhiteSpace(c) && c is not '\'' and not '\\'))
        {
            return value;
        }

        return $"'{value.Replace("\\", "\\\\").Replace("'", "\\'")}'";
    }
}
