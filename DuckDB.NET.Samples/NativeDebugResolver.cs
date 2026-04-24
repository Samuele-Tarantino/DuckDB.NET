using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class NativeDebugResolver
{
    private static IntPtr _duckdbHandle;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        //string repo = @"C:\Sources\Git\duckdb_scalarfs";
        string repo = @"C:\Users\Lucap\Downloads\duckdb 5";
        string duckdbDir = Path.Combine(repo, "build", "debug", "src", "Debug");

        string duckdbPath = Path.Combine(duckdbDir, "duckdb.dll");

        if (!File.Exists(duckdbPath)) throw new FileNotFoundException("duckdb.dll not found", duckdbPath);

        // Optional but useful: preload debug runtime deps near duckdb.dll
        //PreloadIfExists(Path.Combine(duckdbDir, "zlibd1.dll"));
        //PreloadIfExists(Path.Combine(duckdbDir, "libcrypto-3-x64.dll"));
        //PreloadIfExists(Path.Combine(duckdbDir, "libssl-3-x64.dll"));
        //PreloadIfExists(Path.Combine(duckdbDir, "libcurl-d.dll"));

        _duckdbHandle = NativeLibrary.Load(duckdbPath);

        // Install resolver for the assembly that contains your DllImport declarations.
        // If your DllImports are in another assembly, pass that assembly instead.
        NativeLibrary.SetDllImportResolver(
            typeof(NativeDebugResolver).Assembly,
            Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (IsDuckDbName(libraryName))
        {
            return _duckdbHandle;
        }

        return IntPtr.Zero;
    }

    private static bool IsDuckDbName(string name)
    {
        return name.Equals("duckdb", StringComparison.OrdinalIgnoreCase)
            || name.Equals("duckdb.dll", StringComparison.OrdinalIgnoreCase);
    }

    private static void PreloadIfExists(string path)
    {
        if (File.Exists(path))
        {
            NativeLibrary.Load(path);
        }
    }

}