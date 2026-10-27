namespace DuckDB.NET.Data.Profiling
{
    public static class DuckDBMetrics
    {
        /// <summary>The metrics tracked when <see cref="ProfilingOptions.TrackedMetrics"/> is not set.</summary>
        public static readonly IReadOnlyList<string> DefaultTrackedMetrics =
        [
            // Query
            DuckDBMetricNames.QuerySql,
            DuckDBMetricNames.QueryTotalTime,
            DuckDBMetricNames.QueryCpuTime,
            DuckDBMetricNames.QueryTotalIntermediateRows,
            DuckDBMetricNames.QueryTotalRowsScanned,

            // Operators (also used to compute ProfilingInfoMetrics.ResultSetSizeBytes)
            DuckDBMetricNames.OperatorType,
            DuckDBMetricNames.OperatorExtraInfo,
            DuckDBMetricNames.OperatorIntermediateRows,
            DuckDBMetricNames.OperatorIntermediateSizeBytes,
            DuckDBMetricNames.OperatorRowsScanned,
            DuckDBMetricNames.OperatorTiming,

            // System / IO
            DuckDBMetricNames.SystemBlockedThreadTime,
            DuckDBMetricNames.SystemTotalMemoryAllocated,
            DuckDBMetricNames.SystemPeakBufferMemory,
            DuckDBMetricNames.SystemPeakTempDirSize,
            DuckDBMetricNames.IoTotalBytesRead,
            DuckDBMetricNames.IoTotalBytesWritten,

            // WAL / attach / checkpoint
            DuckDBMetricNames.StorageAttachLoadStorageLatency,
            DuckDBMetricNames.StorageAttachReplayWalLatency,
            DuckDBMetricNames.StorageCheckpointLatency,
            DuckDBMetricNames.StorageCommitLocalStorageLatency,
            DuckDBMetricNames.StorageWaitingToAttachLatency,
            DuckDBMetricNames.StorageWalReplayEntryCount,
            DuckDBMetricNames.StorageWriteToWalLatency
        ];
    }
}
