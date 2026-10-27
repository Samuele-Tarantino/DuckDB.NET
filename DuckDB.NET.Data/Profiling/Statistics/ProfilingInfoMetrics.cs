namespace DuckDB.NET.Data.Profiling.Statistics;

/// <summary>
/// The metrics collected for a statement, by DuckDB 2.0 metric name (see <see cref="DuckDBMetricNames"/>), e.g.
/// <c>query.total_time</c>, with the raw value DuckDB reported.
/// </summary>
public sealed class ProfilingInfoMetrics : Dictionary<string, string>
{
    /// <summary>
    /// The size in bytes of the result set: the output of the root operator of the plan. Available when
    /// <see cref="DuckDBMetricNames.OperatorType"/> and <see cref="DuckDBMetricNames.OperatorIntermediateSizeBytes"/> are tracked
    /// (both are in <see cref="DuckDBMetrics.DefaultTrackedMetrics"/>), otherwise null.
    /// </summary>
    public ulong? ResultSetSizeBytes { get; private init; }

    internal static ProfilingInfoMetrics FromTree(ProfilingTreeMetrics? tree)
    {
        if (tree == null)
        {
            return [];
        }

        var result = new ProfilingInfoMetrics
        {
            ResultSetSizeBytes = ulong.TryParse(tree.ResultSetSize, out var size) ? size : null
        };

        foreach (var (name, value) in tree.ByName)
        {
            result[name] = value;
        }

        return result;
    }
}
