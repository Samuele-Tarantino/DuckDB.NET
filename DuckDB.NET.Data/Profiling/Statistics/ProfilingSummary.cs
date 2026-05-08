using System;
using System.Collections.Generic;
using DuckDB.NET.Data.Profiling;

namespace DuckDB.NET.Data.Profiling.Statistics;

public readonly record struct ProfilingSummary(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    double ConnectionTimeMilliseconds,
    double ExecutionTimeMilliseconds,
    int MetricsCount,
    ProfilingInfoMetrics[] Metrics)
{
    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>(6)
        {
            ["StartTime"] = StartTime,
            ["EndTime"] = EndTime,
            ["ConnectionTime"] = ConnectionTimeMilliseconds,
            ["ExecutionTime"] = ExecutionTimeMilliseconds,
            ["MetricsCount"] = MetricsCount,
            ["Metrics"] = Metrics
        };
    }
}
