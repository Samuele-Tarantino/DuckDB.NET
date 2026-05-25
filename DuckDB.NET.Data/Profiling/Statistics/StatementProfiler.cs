using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal sealed class StatementProfiler(int queryIndex, DuckDBNativeConnection connection)
: ExecutionProfiler(connection)
{
    private Dictionary<string, string> rawMetrics = [];

    /// <summary>
    /// Gets the profiling metrics associated with the current operation.
    /// </summary>
    internal ProfilingInfoMetrics Info => ProfilingInfoMetrics.FromRawMetrics(rawMetrics);

    /// <summary>
    /// Creates a summary of the profiling statement, including execution timing, order, metrics, state, and any associated
    /// message.
    /// </summary>
    /// <returns>A <see cref="ProfilingStatementSummary"/> instance containing the collected profiling data for the statement.</returns>
    internal ProfilingStatementSummary GetSummary()
    {
        return new ProfilingStatementSummary(
            StartTime: startExecutionTime,
            EndTime: endExecutionTime,
            ExecutionTimeMilliseconds: TimerUtils.TimerToMilliseconds(executionTime),
            Order: queryIndex,
            Metrics: Info,
            State: state,
            Message: ErrorMessage);
    }

    /// <inheritdoc/>
    public override void AcquireMetrics()
    {
        var profile = new ProfilingInfo(duckDBNativeConnection);

        if (profile.TryPrepare())
        {
            rawMetrics = profile.GetRawMetrics();
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        executionTime = 0;
        startExecutionTimestamp = null;
        startExecutionTime = default;
        endExecutionTime = default;
    }
}
