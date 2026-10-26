namespace DuckDB.NET.Data.Profiling;

/// <summary>
/// Names of the profiling metrics DuckDB 2.0 reports (see <c>SELECT * FROM duckdb_available_metrics()</c>).
/// Use them with <see cref="ProfilingOptions.TrackedMetrics"/> and as keys of <see cref="Statistics.ProfilingInfoMetrics.ByName"/>.
/// Metric names are grouped by a prefix and a glob such as <c>query.*</c> selects a whole group.
/// </summary>
public static class DuckDBMetricNames
{
    /// <summary>Every metric.</summary>
    public const string All = "*";

    public const string QueryAll = "query.*";
    public const string QueryCpuTime = "query.cpu_time";
    public const string QuerySql = "query.sql";
    public const string QueryTotalIntermediateRows = "query.total_intermediate_rows";
    public const string QueryTotalIntermediateSizeBytes = "query.total_intermediate_size_bytes";
    public const string QueryTotalRowGroupsScanned = "query.total_row_groups_scanned";
    public const string QueryTotalRowGroupsToScan = "query.total_row_groups_to_scan";
    public const string QueryTotalRowsScanned = "query.total_rows_scanned";
    public const string QueryTotalTime = "query.total_time";

    public const string SystemAll = "system.*";
    public const string SystemBlockedThreadTime = "system.blocked_thread_time";
    public const string SystemPeakBufferMemory = "system.peak_buffer_memory";
    public const string SystemPeakStreamingBufferSize = "system.peak_streaming_buffer_size";
    public const string SystemPeakTempDirSize = "system.peak_temp_dir_size";
    public const string SystemTotalMemoryAllocated = "system.total_memory_allocated";

    public const string IoAll = "io.*";
    public const string IoTotalBytesRead = "io.total_bytes_read";
    public const string IoTotalBytesWritten = "io.total_bytes_written";

    public const string StorageAll = "storage.*";
    public const string StorageAttachLoadStorageLatency = "storage.attach_load_storage_latency";
    public const string StorageAttachReplayWalLatency = "storage.attach_replay_wal_latency";
    public const string StorageCheckpointLatency = "storage.checkpoint_latency";
    public const string StorageCommitLocalStorageLatency = "storage.commit_local_storage_latency";
    public const string StorageTotalVacuumTime = "storage.total_vacuum_time";
    public const string StorageWaitingToAttachLatency = "storage.waiting_to_attach_latency";
    public const string StorageWalReplayEntryCount = "storage.wal_replay_entry_count";
    public const string StorageWriteToWalLatency = "storage.write_to_wal_latency";

    public const string ParserTotalTime = "parser.total_time";

    public const string PlannerAll = "planner.*";
    public const string PlannerBindingTime = "planner.binding_time";
    public const string PlannerTotalTime = "planner.total_time";

    public const string PhysicalPlannerAll = "physical_planner.*";
    public const string PhysicalPlannerColumnBinding = "physical_planner.column_binding";
    public const string PhysicalPlannerCreatePlan = "physical_planner.create_plan";
    public const string PhysicalPlannerResolveTypes = "physical_planner.resolve_types";
    public const string PhysicalPlannerTotalTime = "physical_planner.total_time";

    public const string OptimizerAll = "optimizer.*";
    public const string OptimizerTotalTime = "optimizer.total_time";

    /// <summary>Metrics of the operator tree. They are not reported per query: they are tracked to compute the legacy ResultSetSize metric.</summary>
    public const string OperatorAll = "operator.*";
    public const string OperatorCpuTime = "operator.cpu_time";
    public const string OperatorExtraInfo = "operator.extra_info";
    public const string OperatorIntermediateRows = "operator.intermediate_rows";
    public const string OperatorIntermediateSizeBytes = "operator.intermediate_size_bytes";
    public const string OperatorRowGroupsScanned = "operator.row_groups_scanned";
    public const string OperatorRowsScanned = "operator.rows_scanned";
    public const string OperatorTiming = "operator.timing";
    public const string OperatorTotalRowGroupsToScan = "operator.total_row_groups_to_scan";
    public const string OperatorType = "operator.type";
}
