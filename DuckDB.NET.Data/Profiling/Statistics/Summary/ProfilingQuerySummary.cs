namespace DuckDB.NET.Data.Profiling.Statistics.Summary;

public readonly record struct ProfilingQuerySummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ExecutionTimeMilliseconds,
    int StatementCount,
    ProfilingStatementSummary[] Infos)
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
