
namespace DuckDB.NET.Native;

public partial class NativeMethods
{
    //https://duckdb.org/docs/current/clients/c/api#profiling-info
    public static partial class ProfilingInfo
    {

        [LibraryImport(DuckDbLibrary, EntryPoint = "duckdb_get_profiling_info")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial DuckDBProfilingInfo DuckDBGetProfilingInfo(DuckDBNativeConnection connection);

        [LibraryImport(DuckDbLibrary, EntryPoint = "duckdb_profiling_info_get_value")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial DuckDBValue DuckDBProfilingInfoGetValue(DuckDBProfilingInfo info, [MarshalAs(UnmanagedType.LPStr)] string key);

        [LibraryImport(DuckDbLibrary, EntryPoint = "duckdb_profiling_info_get_metrics")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial DuckDBValue DuckDBProfilingInfoGetMetrics(DuckDBProfilingInfo info);

        [LibraryImport(DuckDbLibrary, EntryPoint = "duckdb_profiling_info_get_child_count")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial ulong DuckDBProfilingInfoGetChildCount(DuckDBProfilingInfo info);

        [LibraryImport(DuckDbLibrary, EntryPoint = "duckdb_profiling_info_get_child")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial DuckDBProfilingInfo DuckDBProfilingInfoGetChild(DuckDBProfilingInfo info, ulong index);

    }
}