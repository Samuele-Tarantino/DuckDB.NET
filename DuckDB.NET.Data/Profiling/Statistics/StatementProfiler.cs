using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal sealed class StatementProfiler(int queryIndex, DuckDBNativeConnection connection, ProfilingOptions? profilingOptions)
: ExecutionProfiler(connection, profilingOptions)
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
        // If a metrics threshold is set, check if the elapsed execution time meets the threshold before acquiring metrics.
        if ((profilingOptions?.MetricsThresholdMS ?? 0) > 0)
        {
            long elapsed = TimerUtils.CalculateTickCountElapsed(startExecutionTimestamp ?? 0, TimerUtils.TimerCurrent());

            if (TimerUtils.TimerToMilliseconds(elapsed) < (profilingOptions?.MetricsThresholdMS ?? 0))
                return;
        }

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
