namespace DuckDB.NET.Data.Profiling.Statistics.Summary;

public readonly record struct ProfilingStatementSummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ExecutionTimeMilliseconds,
    int Order,
    ProfilingInfoMetrics Metrics,
    DuckDBState State,
    string Message)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(6)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["Order"] = Order,
            ["Infos"] = Metrics,
            ["State"] = State,
            ["Message"] = Message
        };
    }
}
