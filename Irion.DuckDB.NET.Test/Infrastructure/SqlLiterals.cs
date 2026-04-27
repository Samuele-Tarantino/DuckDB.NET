namespace Irion.DuckDB.NET.Test.Infrastructure;

public static class SqlLiterals
{
    public static string DuckDbString(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }
}
