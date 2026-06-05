namespace DuckDB.NET.Data.Profiling.Statistics;


public sealed class ProfilingInfoMetrics : Dictionary<DuckDBMetricType, object>
{
    internal static ProfilingInfoMetrics FromRawMetrics(Dictionary<string, string> dict)
    {
        ArgumentNullException.ThrowIfNull(dict);

        ProfilingInfoMetrics result = [];

        foreach (var kvp in dict)
        {
            if (DuckDBMetricsExtensions.TryParseDuckDBMetricType(kvp.Key, out var metricType))
            {
                result.Add(metricType, kvp.Value);
            }
        }

        return result;
    }
}
