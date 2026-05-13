namespace DuckDB.NET.Data.Profiling.Statistics;


public sealed class ProfilingInfoMetrics : Dictionary<DuckDBMetricType, object>
{
    internal static ProfilingInfoMetrics FromMetricsDictionary(IDictionary<string, object> dict)
    {
        ArgumentNullException.ThrowIfNull(dict);

        ProfilingInfoMetrics result = [];

        foreach (var kvp in dict)
        {
            if (MetricsExtensions.TryParseDuckDBMetricType(kvp.Key, out var metricType))
            {
                result.Add(metricType, kvp.Value);
            }
        }

        return result;
    }
}
