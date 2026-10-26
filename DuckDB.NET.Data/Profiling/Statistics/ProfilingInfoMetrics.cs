#pragma warning disable CS0618 // The legacy DuckDBMetricType keys are kept for existing callers.

namespace DuckDB.NET.Data.Profiling.Statistics;

/// <summary>
/// The metrics collected for a statement. As a dictionary it is keyed by the legacy <see cref="DuckDBMetricType"/>;
/// <see cref="ByName"/> holds every metric under its DuckDB 2.0 name (see <see cref="DuckDBMetricNames"/>).
/// </summary>
public sealed class ProfilingInfoMetrics : Dictionary<DuckDBMetricType, object>
{
    private static readonly IReadOnlyDictionary<string, string> NoMetrics = new Dictionary<string, string>();

    /// <summary>The metrics by DuckDB 2.0 name, e.g. <c>query.total_time</c>, with the raw value DuckDB reported.</summary>
    public IReadOnlyDictionary<string, string> ByName { get; private init; } = NoMetrics;

    internal static ProfilingInfoMetrics FromTree(ProfilingTreeMetrics? tree)
    {
        if (tree == null)
        {
            return [];
        }

        ProfilingInfoMetrics result = new() { ByName = tree.ByName };

        foreach (var (name, value) in tree.ByName)
        {
            if (MetricMapping.TryGetLegacy(name, out var metricType))
            {
                result[metricType] = value;
            }
        }

        // DuckDB 2.0 has no result set size metric: it is the output size of the root operator of the plan.
        if (tree.ResultSetSize != null)
        {
            result[DuckDBMetricType.ResultSetSize] = tree.ResultSetSize;
        }

        return result;
    }
}
