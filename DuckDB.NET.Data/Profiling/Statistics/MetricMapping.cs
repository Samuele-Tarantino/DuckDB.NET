#pragma warning disable CS0618 // The legacy DuckDBMetricType is what this class translates from and to.

using System.Collections.Frozen;
using System.Linq;
using System.Text.Json;

namespace DuckDB.NET.Data.Profiling.Statistics;

/// <summary>
/// Translates between the legacy <see cref="DuckDBMetricType"/> enum (DuckDB 1.x metric names) and the metric
/// names of DuckDB 2.0, and flattens the 2.0 profiling tree.
/// </summary>
internal static class MetricMapping
{
    internal const string OperatorIntermediateSizeBytes = DuckDBMetricNames.OperatorIntermediateSizeBytes;

    private static readonly FrozenDictionary<DuckDBMetricType, string> LegacyToName = BuildLegacyToName();

    private static readonly FrozenDictionary<string, DuckDBMetricType> NameToLegacy =
        LegacyToName.Where(kv => !kv.Value.StartsWith("operator.", StringComparison.Ordinal) && !kv.Value.EndsWith(".*", StringComparison.Ordinal))
                    .ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

    // Metrics of every group but the operator tree and the optimizers, by group. A node of the profiling tree carries the
    // metrics of one group without the group prefix, so the group has to be recovered from the keys.
    private static readonly FrozenDictionary<string, string[]> GroupKeys = new Dictionary<string, string[]>
    {
        ["query"] = ["cpu_time", "sql", "total_intermediate_rows", "total_intermediate_size_bytes", "total_row_groups_scanned", "total_row_groups_to_scan", "total_rows_scanned", "total_time"],
        ["physical_planner"] = ["column_binding", "create_plan", "resolve_types", "total_time"],
        ["system"] = ["blocked_thread_time", "peak_buffer_memory", "peak_streaming_buffer_size", "peak_temp_dir_size", "total_memory_allocated"],
        ["planner"] = ["binding_time", "total_time"],
        ["io"] = ["total_bytes_read", "total_bytes_written"],
        ["storage"] = ["attach_load_storage_latency", "attach_replay_wal_latency", "checkpoint_latency", "commit_local_storage_latency", "total_vacuum_time", "waiting_to_attach_latency", "wal_replay_entry_count", "write_to_wal_latency"],
        ["parser"] = ["total_time"],
    }.ToFrozenDictionary();

    // The order DuckDB lists the groups in when several of them can explain the same keys (only "total_time" is shared).
    private static readonly string[] GroupOrder = ["query", "optimizer", "physical_planner", "system", "planner", "io", "storage", "parser"];

    private static FrozenDictionary<DuckDBMetricType, string> BuildLegacyToName()
    {
        var map = new Dictionary<DuckDBMetricType, string>
        {
            [DuckDBMetricType.QueryName] = DuckDBMetricNames.QuerySql,
            [DuckDBMetricType.Latency] = DuckDBMetricNames.QueryTotalTime,
            [DuckDBMetricType.CpuTime] = DuckDBMetricNames.QueryCpuTime,
            [DuckDBMetricType.CumulativeCardinality] = DuckDBMetricNames.QueryTotalIntermediateRows,
            [DuckDBMetricType.CumulativeRowsScanned] = DuckDBMetricNames.QueryTotalRowsScanned,
            [DuckDBMetricType.ResultSetSize] = OperatorIntermediateSizeBytes,
            [DuckDBMetricType.ExtraInfo] = DuckDBMetricNames.OperatorExtraInfo,

            [DuckDBMetricType.BlockedThreadTime] = DuckDBMetricNames.SystemBlockedThreadTime,
            [DuckDBMetricType.SystemPeakBufferMemory] = DuckDBMetricNames.SystemPeakBufferMemory,
            [DuckDBMetricType.SystemPeakTempDirSize] = DuckDBMetricNames.SystemPeakTempDirSize,
            [DuckDBMetricType.TotalMemoryAllocated] = DuckDBMetricNames.SystemTotalMemoryAllocated,

            [DuckDBMetricType.TotalBytesRead] = DuckDBMetricNames.IoTotalBytesRead,
            [DuckDBMetricType.TotalBytesWritten] = DuckDBMetricNames.IoTotalBytesWritten,

            [DuckDBMetricType.AttachLoadStorageLatency] = DuckDBMetricNames.StorageAttachLoadStorageLatency,
            [DuckDBMetricType.AttachReplayWalLatency] = DuckDBMetricNames.StorageAttachReplayWalLatency,
            [DuckDBMetricType.CheckpointLatency] = DuckDBMetricNames.StorageCheckpointLatency,
            [DuckDBMetricType.CommitLocalStorageLatency] = DuckDBMetricNames.StorageCommitLocalStorageLatency,
            [DuckDBMetricType.WaitingToAttachLatency] = DuckDBMetricNames.StorageWaitingToAttachLatency,
            [DuckDBMetricType.WalReplayEntryCount] = DuckDBMetricNames.StorageWalReplayEntryCount,
            [DuckDBMetricType.WriteToWalLatency] = DuckDBMetricNames.StorageWriteToWalLatency,

            [DuckDBMetricType.OperatorCardinality] = DuckDBMetricNames.OperatorIntermediateRows,
            [DuckDBMetricType.OperatorRowsScanned] = DuckDBMetricNames.OperatorRowsScanned,
            [DuckDBMetricType.OperatorTiming] = DuckDBMetricNames.OperatorTiming,
            [DuckDBMetricType.OperatorType] = DuckDBMetricNames.OperatorType,

            [DuckDBMetricType.AllOptimizers] = DuckDBMetricNames.OptimizerAll,
            [DuckDBMetricType.CumulativeOptimizerTiming] = DuckDBMetricNames.OptimizerTotalTime,
            [DuckDBMetricType.PhysicalPlanner] = DuckDBMetricNames.PhysicalPlannerTotalTime,
            [DuckDBMetricType.PhysicalPlannerColumnBinding] = DuckDBMetricNames.PhysicalPlannerColumnBinding,
            [DuckDBMetricType.PhysicalPlannerCreatePlan] = DuckDBMetricNames.PhysicalPlannerCreatePlan,
            [DuckDBMetricType.PhysicalPlannerResolveTypes] = DuckDBMetricNames.PhysicalPlannerResolveTypes,
            [DuckDBMetricType.Planner] = DuckDBMetricNames.PlannerTotalTime,
            [DuckDBMetricType.PlannerBinding] = DuckDBMetricNames.PlannerBindingTime,

            // Not reported by DuckDB 2.0: RowsReturned (never available) and OperatorName (2.0 only reports the operator type).
        };

        // OptimizerFilterPullup -> optimizer.filter_pullup; the sum rewriter became the aggregate function rewriter.
        foreach (var metric in Enum.GetValues<DuckDBMetricType>())
        {
            var name = metric.ToString();
            if (name.StartsWith("Optimizer", StringComparison.Ordinal) && !map.ContainsKey(metric))
            {
                map[metric] = "optimizer." + JsonNamingPolicy.SnakeCaseLower.ConvertName(name["Optimizer".Length..]);
            }
        }

        map[DuckDBMetricType.OptimizerSumRewriter] = "optimizer.aggregate_function_rewriter";

        return map.ToFrozenDictionary();
    }

    /// <summary>The DuckDB 2.0 name of a legacy metric, or null when 2.0 does not report it.</summary>
    internal static string? ToName(DuckDBMetricType metric)
    {
        if (metric == DuckDBMetricType.RowsReturned)
        {
            throw new DuckDBException($"The '{DuckDBMetricType.RowsReturned}' metric is not supported yet.", DuckDBErrorType.InvalidInput);
        }

        return LegacyToName.GetValueOrDefault(metric);
    }

    internal static bool TryGetLegacy(string name, out DuckDBMetricType metric) => NameToLegacy.TryGetValue(name, out metric);

    /// <summary>The names to track for the given options, de-duplicated and in a stable order.</summary>
    internal static List<string> ToTrackedNames(IEnumerable<DuckDBMetricType>? legacyMetrics, IEnumerable<string>? trackedMetrics)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
            {
                names.Add(name);
            }
        }

        if (legacyMetrics != null)
        {
            foreach (var metric in legacyMetrics)
            {
                Add(ToName(metric));

                // The size of the result set is read from the root operator, which is found below the result collector.
                if (metric == DuckDBMetricType.ResultSetSize)
                {
                    Add(DuckDBMetricNames.OperatorType);
                }
            }
        }

        if (trackedMetrics != null)
        {
            foreach (var name in trackedMetrics)
            {
                Add(name);
            }
        }

        return names;
    }

    /// <summary>Whether <paramref name="name"/> is selected by one of the tracked names or globs (<c>*</c>, <c>group.*</c>).</summary>
    private static bool IsTracked(IReadOnlyList<string>? tracked, string name)
    {
        if (tracked == null)
        {
            return true;
        }

        foreach (var pattern in tracked)
        {
            if (pattern == "*" || pattern == name ||
                (pattern.EndsWith('*') && name.StartsWith(pattern.AsSpan(0, pattern.Length - 1), StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Flattens the profiling tree DuckDB 2.0 returns (an empty root with one child per metric group plus the operator tree)
    /// into qualified metric names, e.g. <c>query.total_time</c>, and computes the size of the result set from the operator tree.
    /// </summary>
    internal static ProfilingTreeMetrics Flatten(DuckDBProfilingInfoWrapper root, IReadOnlyList<string>? tracked)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        string? resultSetSize = null;

        for (ulong i = 0; i < root.GetChildCount(); i++)
        {
            using var child = root.GetChild(i);

            if (child.GetChildCount() > 0 || IsOperatorNode(child))
            {
                resultSetSize ??= ReadResultSetSize(child);
                continue;
            }

            using var metricsValue = child.GetMetrics();
            if (metricsValue.IsNull())
            {
                continue;
            }

            var metrics = metricsValue.GetMapValue<string, string>();
            if (metrics.Count == 0)
            {
                continue;
            }

            var group = ResolveGroup(metrics.Keys, assigned, tracked);
            if (group == null)
            {
                continue;
            }

            assigned.Add(group);
            foreach (var (key, value) in metrics)
            {
                values[group + "." + key] = value;
            }
        }

        return new ProfilingTreeMetrics(values, resultSetSize);
    }

    private static bool IsOperatorNode(DuckDBProfilingInfoWrapper node)
    {
        using var metricsValue = node.GetMetrics();
        if (metricsValue.IsNull())
        {
            return false;
        }

        var metrics = metricsValue.GetMapValue<string, string>();
        return metrics.ContainsKey("type") || metrics.ContainsKey("timing");
    }

    // The size of the result set is the output of the root operator of the plan. Executing a prepared statement wraps the plan
    // in a result collector and an EXECUTE node, which produce no output themselves.
    private static string? ReadResultSetSize(DuckDBProfilingInfoWrapper operatorNode)
    {
        var node = operatorNode;
        var ownsNode = false;

        try
        {
            while (true)
            {
                using var metricsValue = node.GetMetrics();
                var metrics = metricsValue.IsNull() ? [] : metricsValue.GetMapValue<string, string>();

                if (metrics.GetValueOrDefault("type") is not ("RESULT_COLLECTOR" or "EXECUTE") || node.GetChildCount() == 0)
                {
                    return metrics.GetValueOrDefault("intermediate_size_bytes");
                }

                var child = node.GetChild(0);
                if (ownsNode)
                {
                    node.Dispose();
                }

                node = child;
                ownsNode = true;
            }
        }
        finally
        {
            if (ownsNode)
            {
                node.Dispose();
            }
        }
    }

    private static string? ResolveGroup(IReadOnlyCollection<string> keys, HashSet<string> assigned, IReadOnlyList<string>? tracked)
    {
        foreach (var group in GroupOrder)
        {
            if (assigned.Contains(group) || !Explains(group, keys))
            {
                continue;
            }

            if (keys.Any(key => IsTracked(tracked, group + "." + key)))
            {
                return group;
            }
        }

        return null;
    }

    private static bool Explains(string group, IReadOnlyCollection<string> keys)
    {
        if (GroupKeys.TryGetValue(group, out var groupKeys))
        {
            return keys.All(key => Array.IndexOf(groupKeys, key) >= 0);
        }

        // Optimizer: its total plus one entry per optimizer, so any key no other group reports.
        return keys.All(key => key == "total_time" || !GroupKeys.Values.Any(other => Array.IndexOf(other, key) >= 0));
    }
}

internal sealed record ProfilingTreeMetrics(Dictionary<string, string> ByName, string? ResultSetSize);
