namespace DuckDB.NET.Data.Profiling.Statistics.Summary;

public readonly record struct ProfilingQuerySummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ExecutionTimeMilliseconds,
    int StatementCount,
    ProfilingStatementSummary[] StatementSummaries,
    DuckDBState State,
    string Message)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(7)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["StatementCount"] = StatementCount,
            ["StatementSummaries"] = StatementSummaries,
            ["State"] = State,
            ["Message"] = Message
        };
    }
}
