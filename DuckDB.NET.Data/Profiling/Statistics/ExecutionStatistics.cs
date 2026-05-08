namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal abstract class ExecutionStatistics(DuckDBNativeConnection duckDBNativeConnection): ExecutionStatisticsBase(duckDBNativeConnection)
    {
        internal abstract void AcquireProfilingInfo();
    }
}