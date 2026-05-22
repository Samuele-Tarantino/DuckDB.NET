namespace DuckDB.NET.Data.Profiling.Statistics.Summary;

public readonly record struct ProfilingSummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ConnectionTimeMilliseconds,
    double ExecutionTimeMilliseconds,
    int QueryCount,
    ProfilingQuerySummary[] QuerySummaryList)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(6)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ConnectionTime"] = ConnectionTimeMilliseconds,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["QueryCount"] = QueryCount,
            ["QuerySummaryList"] = QuerySummaryList
        };
    }
}
