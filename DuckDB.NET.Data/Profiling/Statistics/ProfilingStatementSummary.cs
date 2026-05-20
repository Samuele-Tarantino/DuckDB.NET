namespace DuckDB.NET.Data.Profiling.Statistics;

public readonly record struct ProfilingStatementSummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ExecutionTimeMilliseconds,
    int Order,
    ProfilingInfoMetrics Metrics)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(6)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["Order"] = Order,
            ["Infos"] = Metrics
        };
    }
}
