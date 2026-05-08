namespace Irion.DuckDB.NET.Test.MsSql;

public static class MsSqlDuckDbExtension
{
    private const string ExtensionName = "mssql";
    private const string ExtensionRepositoryEnvironmentVariable = "IRION_DUCKDB_MSSQL_EXTENSION_REPOSITORY";
    private const string DefaultExtensionRepository = @"\\archsrv01\Shared\duckdb-extension";

    private static readonly Lazy<string> ExtensionDirectory = new(CreateExtensionDirectory);

    public static string ConnectionStringOptions()
    {
        return string.Join(
            ";",
            $"allow_unsigned_extensions=true",
            $"extension_directory={ExtensionDirectory.Value}");
    }

    public static string InstallLoadAndSecureSql()
    {
        return
            $"""
            SET custom_extension_repository = {SqlLiterals.DuckDbString(GetExtensionRepository())};
            INSTALL {ExtensionName};
            LOAD {ExtensionName};
            SET allow_unsigned_extensions = false;
            """;
    }

    private static string GetExtensionRepository()
    {
        var configuredRepository = Environment.GetEnvironmentVariable(ExtensionRepositoryEnvironmentVariable);

        return string.IsNullOrWhiteSpace(configuredRepository)
            ? DefaultExtensionRepository
            : configuredRepository;
    }

    private static string CreateExtensionDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "duckdb-net-irion-extensions");
        Directory.CreateDirectory(path);
        return path;
    }
}
