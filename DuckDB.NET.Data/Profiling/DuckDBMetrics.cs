using System.Linq;

namespace DuckDB.NET.Data.Profiling
{
    public static class DuckDBMetrics
    {
        public static readonly DuckDBMetricTypeCollection AllMetrics = new DuckDBMetricTypeCollection(Enum.GetValues<DuckDBMetricType>().Cast<DuckDBMetricType>());

        public static readonly DuckDBMetricTypeCollection DefaultMetrics = new DuckDBMetricTypeCollection(
        [
            // Basic query metadata
            DuckDBMetricType.QueryName,
            DuckDBMetricType.Latency,
            DuckDBMetricType.CpuTime,
            DuckDBMetricType.BlockedThreadTime,

            // Result and cardinality
            DuckDBMetricType.ResultSetSize,
            // DuckDBMetricType.RowsReturned, // Not enabled due to known issue
            DuckDBMetricType.CumulativeCardinality,
            DuckDBMetricType.CumulativeRowsScanned,
            DuckDBMetricType.ExtraInfo,

            // Operator-level metrics
            DuckDBMetricType.OperatorCardinality,
            DuckDBMetricType.OperatorName,
            DuckDBMetricType.OperatorRowsScanned,
            DuckDBMetricType.OperatorTiming,
            DuckDBMetricType.OperatorType,

            // Memory / IO / storage metrics
            DuckDBMetricType.TotalBytesRead,
            DuckDBMetricType.TotalBytesWritten,
            DuckDBMetricType.TotalMemoryAllocated,
            DuckDBMetricType.SystemPeakBufferMemory,
            DuckDBMetricType.SystemPeakTempDirSize,

            // WAL / attach / checkpoint metrics
            DuckDBMetricType.AttachLoadStorageLatency,
            DuckDBMetricType.AttachReplayWalLatency,
            DuckDBMetricType.CheckpointLatency,
            DuckDBMetricType.CommitLocalStorageLatency,
            DuckDBMetricType.WaitingToAttachLatency,
            DuckDBMetricType.WalReplayEntryCount,
            DuckDBMetricType.WriteToWalLatency
        ]);
    }
}