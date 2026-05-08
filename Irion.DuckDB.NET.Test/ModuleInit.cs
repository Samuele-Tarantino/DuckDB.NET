namespace Irion.DuckDB.NET.Test;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init()
    {
        NativeLibraryLoader.TryLoadDuckDb();
    }
}
