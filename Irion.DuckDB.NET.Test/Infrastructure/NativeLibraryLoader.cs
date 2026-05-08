namespace Irion.DuckDB.NET.Test.Infrastructure;

internal static class NativeLibraryLoader
{
    public static bool TryLoadDuckDb()
    {
        if (GetRid() is not { } rid)
        {
            return false;
        }

        return NativeLibrary.TryLoad(Path.Join("runtimes", rid, "native", "duckdb"), typeof(NativeLibraryLoader).Assembly, DllImportSearchPath.AssemblyDirectory, out _) ||
               NativeLibrary.TryLoad(Path.Join("runtimes", rid, "native", "libduckdb"), typeof(NativeLibraryLoader).Assembly, DllImportSearchPath.AssemblyDirectory, out _);
    }

    private static string? GetRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.Is64BitProcess ? "win-x64" : "win-x86";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => null,
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }

        return null;
    }
}
