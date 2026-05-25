using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;
using System.Linq;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal sealed class QueryProfiler(IntPtr queryIdentifier, int statementCount, DuckDBNativeConnection connection)
: ExecutionProfiler(connection)
{
    private readonly StatementProfiler[] statementProfilers = new StatementProfiler[statementCount];


    internal int StatementCount => statementCount;

    internal IntPtr QueryIdentifier => queryIdentifier;

    internal StatementProfiler CreateStatementProfiler(int index)
    {
        var profiler = new StatementProfiler(index, duckDBNativeConnection);
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
             startExecutionTime,
             endExecutionTime,
             TimerUtils.TimerToMilliseconds(executionTime),
             statementCount,
             [.. statementProfilers.Select(sp => sp.GetSummary())],
             state,
             errorMessage
            );
    }

    /// <summary>
    /// Stops the timer associated with the specified statement index.
    /// </summary>
    /// <param name="statementIndex">The zero-based index of the statement whose timer should be stopped.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if statementIndex is less than zero or greater than the highest valid statement profiler index.</exception>
    public void StopTimer(int statementIndex)
    {
        if (statementIndex < 0 || statementIndex > statementProfilers.Length - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(statementIndex), $"Index {statementIndex} is out of range for statement profilers.");
        }
        statementProfilers[statementIndex]?.StopTimer();

        StopTimer();
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
