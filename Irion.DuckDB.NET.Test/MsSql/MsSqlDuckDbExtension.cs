namespace Irion.DuckDB.NET.Test.MsSql;

public static class MsSqlDuckDbExtension
{
    private const string ExtensionName = "mssql";
    private const string DuckDbExtensionVersion = "v1.5.2";
    private const string ExtensionRepositoryEnvironmentVariable = "IRION_DUCKDB_MSSQL_EXTENSION_REPOSITORY";
    private const string PackagedExtensionRepositoryDirectoryName = "duckdb-extension-repository";

    private static readonly Lazy<string> ExtensionDirectory = new(CreateExtensionDirectory);

    public static string ConnectionStringOptions()
    {
        return string.Join(
            ";",
            "allow_unsigned_extensions=true",
            $"extension_directory={ExtensionDirectory.Value}");
    }

    public static string InstallLoadAndSecureSql()
    {
        var sql = new StringBuilder();
        var extensionRepository = GetExtensionRepositoryOrSkip();

        sql.AppendLine($"SET custom_extension_repository = {SqlLiterals.DuckDbString(extensionRepository)};");
        sql.AppendLine($"INSTALL {ExtensionName};");
        sql.AppendLine($"LOAD {ExtensionName};");
        sql.AppendLine("SET allow_unsigned_extensions = false;");

        return sql.ToString();
    }

    private static string GetExtensionRepositoryOrSkip()
    {
        var configuredRepository = Environment.GetEnvironmentVariable(ExtensionRepositoryEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(configuredRepository))
        {
            return configuredRepository.Trim();
        }

        var packagedRepository = Path.Combine(AppContext.BaseDirectory, PackagedExtensionRepositoryDirectoryName);
        var packagedExtension = Path.Combine(packagedRepository, DuckDbExtensionVersion, GetDuckDbPlatform(), "mssql.duckdb_extension.gz");

        if (File.Exists(packagedExtension))
        {
            return packagedRepository;
        }

        throw Xunit.Sdk.SkipException.ForSkip(
            $"DuckDB mssql extension was not found at '{packagedExtension}'. Restore package Irion.DuckDb.Extensions.mssql or set {ExtensionRepositoryEnvironmentVariable} to run MSSQL extension tests.");
    }

    private static string GetDuckDbPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "windows_amd64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "linux_amd64";
        }

        throw Xunit.Sdk.SkipException.ForSkip(
            $"DuckDB mssql extension package does not include an artifact for {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}.");
    }

    private static string CreateExtensionDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "duckdb-net-irion-extensions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
