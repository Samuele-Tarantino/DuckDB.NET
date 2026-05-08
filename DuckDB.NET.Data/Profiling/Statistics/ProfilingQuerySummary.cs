namespace DuckDB.NET.Data.Profiling.Statistics;

public readonly record struct ProfilingQuerySummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ExecutionTimeMilliseconds,
    int StatementCount,
    ProfilingInfoMetrics[] Infos)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(6)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["StatementCount"] = StatementCount,
            ["Infos"] = Infos
        };
    }
}
