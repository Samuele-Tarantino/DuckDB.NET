using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;
using System.Linq;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal sealed class QueryProfiler(int queryIdentifier, int statementCount, DuckDBNativeConnection connection, ProfilingOptions? profilingOptions)
: ExecutionProfiler(connection, profilingOptions)
{
    private readonly StatementProfiler[] statementProfilers = new StatementProfiler[statementCount];


    internal int StatementCount => statementCount;

    internal int QueryIdentifier => queryIdentifier;

    internal StatementProfiler CreateStatementProfiler(int index)
    {
        var profiler = new StatementProfiler(index, duckDBNativeConnection, profilingOptions);
        statementProfilers[index] = profiler;
        return profiler;
    }

    /// <summary>
    /// Creates a summary of the profiling query, including execution times, statement information, and state.
    /// </summary>
    /// <returns>A <see cref="ProfilingQuerySummary"/> object containing details about the query's execution, statements, state, and
    /// any error message.</returns>
    internal ProfilingQuerySummary GetSummary()
    {
        var childWithError = statementProfilers.Where(sp => sp?.State == DuckDBState.Error).FirstOrDefault();

        state = state == DuckDBState.Error ? state : (childWithError?.State ?? DuckDBState.Success);
        errorMessage = !string.IsNullOrEmpty(ErrorMessage) ? ErrorMessage : (childWithError?.ErrorMessage ?? string.Empty);

        return new ProfilingQuerySummary(
             startTime,
             endTime,
             TimerUtils.TimerToMilliseconds(executionTime),
             statementCount,
             [.. statementProfilers.Select(sp => sp?.GetSummary() ?? default)],
             state,
             errorMessage
            );
    }

    /// <summary>
    /// Stops all the statement profilers associated with this query profiler.
    /// </summary>
    public override void StopTimer()
    {

        foreach (var profiler in statementProfilers)
        {
            profiler?.StopTimer();
        }

        base.StopTimer();
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        executionTime = 0;
        startTimestamp = null;
        startTime = default;
        endTime = default;
    }
}
