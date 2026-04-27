using System.Data.Common;

namespace Irion.DuckDB.NET.Test.MsSql;

public static class SqlServerContainerExtensions
{
    public static string GetDuckDbConnectionString(this MsSqlContainer container, string database)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = container.GetConnectionString(),
        };

        builder["Database"] = database;
        builder["Encrypt"] = "False";
        builder.Remove("TrustServerCertificate");
        builder.Remove("Trust Server Certificate");

        return builder.ConnectionString;
    }
}
