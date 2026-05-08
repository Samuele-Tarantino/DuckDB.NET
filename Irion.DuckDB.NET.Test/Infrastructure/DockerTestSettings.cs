namespace Irion.DuckDB.NET.Test.Infrastructure;

public static class DockerTestSettings
{
    public const string ExtensionRepositoryEnvironmentVariable = "IRION_DUCKDB_EXTENSION_REPOSITORY";

    public static string? ExtensionRepository =>
        Normalize(Environment.GetEnvironmentVariable(ExtensionRepositoryEnvironmentVariable));

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
