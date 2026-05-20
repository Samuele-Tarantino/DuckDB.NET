using System.Linq;

namespace DuckDB.NET.Data.Profiling
{
    public static class DuckDBMetrics
    {
        public static IEnumerable<DuckDBMetricType> AllMetrics => Enum.GetValues<DuckDBMetricType>().Cast<DuckDBMetricType>();

        public static IEnumerable<DuckDBMetricType> DefaultMetrics =>
        [
            DuckDBMetricType.QueryName,
            DuckDBMetricType.Latency,
            DuckDBMetricType.CpuTime,
            DuckDBMetricType.BlockedThreadTime,
            DuckDBMetricType.ResultSetSize,
            //DuckDBMetricType.RowsReturned,
            DuckDBMetricType.CumulativeRowsScanned,
            DuckDBMetricType.TotalBytesRead,
            DuckDBMetricType.TotalBytesWritten,
            DuckDBMetricType.TotalMemoryAllocated,
            DuckDBMetricType.SystemPeakBufferMemory,
            DuckDBMetricType.SystemPeakTempDirSize,
            DuckDBMetricType.WriteToWalLatency
        ];

    }

}
