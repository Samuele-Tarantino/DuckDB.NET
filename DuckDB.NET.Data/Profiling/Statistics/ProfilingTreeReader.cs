using System.Collections.Frozen;
using System.Linq;

namespace DuckDB.NET.Data.Profiling.Statistics;

/// <summary>
/// Selects the metrics to track and flattens the profiling tree DuckDB 2.0 returns: an empty root with one child per
/// metric group plus the operator tree.
/// </summary>
internal static class ProfilingTreeReader
{
    internal const string OperatorIntermediateSizeBytes = DuckDBMetricNames.OperatorIntermediateSizeBytes;

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

    /// <summary>The names to track, de-duplicated and in a stable order.</summary>
    internal static List<string> ToTrackedNames(IEnumerable<string>? trackedMetrics)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string name)
        {
            if (seen.Add(name))
            {
                names.Add(name);
            }
        }

        if (trackedMetrics != null)
        {
            foreach (var tracked in trackedMetrics)
            {
                var name = tracked?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("TrackedMetrics contains a null or blank metric name.", nameof(trackedMetrics));
                }

                // DuckDB metric names are lower case; normalizing keeps what is sent to DuckDB and what IsTracked matches in agreement.
                Add(name.ToLowerInvariant());
            }
        }

        return names;
    }

    /// <summary>Whether <paramref name="name"/> is selected by one of the tracked names or globs (<c>*</c> matches any run of characters).</summary>
    internal static bool IsTracked(IReadOnlyList<string>? tracked, string name)
    {
        if (tracked == null)
        {
            return true;
        }

        foreach (var pattern in tracked)
        {
            if (MatchesGlob(pattern, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesGlob(ReadOnlySpan<char> pattern, ReadOnlySpan<char> name)
    {
        var star = pattern.IndexOf('*');
        if (star < 0)
        {
            return pattern.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        var head = pattern[..star];
        if (!name.StartsWith(head, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Skip stars that follow each other, then try every split of the rest of the name.
        var rest = pattern[(star + 1)..].TrimStart('*');
        name = name[head.Length..];

        if (rest.IsEmpty)
        {
            return true;
        }

        for (var i = 0; i <= name.Length; i++)
        {
            if (MatchesGlob(rest, name[i..]))
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

        // The collectors that wrap the plan are told apart by the operator type, so the size is only meaningful when both are tracked.
        var canReadResultSetSize = IsTracked(tracked, OperatorIntermediateSizeBytes) && IsTracked(tracked, DuckDBMetricNames.OperatorType);

        for (ulong i = 0; i < root.GetChildCount(); i++)
        {
            using var child = root.GetChild(i);

            if (child.GetChildCount() > 0 || IsOperatorNode(child))
            {
                if (canReadResultSetSize)
                {
                    resultSetSize ??= ReadResultSetSize(child);
                }

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

                var type = metrics.GetValueOrDefault("type");
                if (type == null)
                {
                    // Without the type a collector cannot be told from the root operator: its size would be wrong.
                    return null;
                }

                if (type is not ("RESULT_COLLECTOR" or "EXECUTE") || node.GetChildCount() == 0)
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
