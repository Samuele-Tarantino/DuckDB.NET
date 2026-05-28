using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;
using System.Collections.Concurrent;
using System.Linq;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal sealed class ConnectionStatistics : ExecutionProfiler
{

    // internal values that are not exposed through properties
    private static readonly ConcurrentDictionary<DuckDBNativeConnection, ConnectionStatistics> ByNativeConnection = new();

    private bool enableQueryExecutionTracing;
    private readonly ConcurrentDictionary<IntPtr, QueryProfiler> queryProfilers = new();
    private bool isDisposed = false;

    // internal values that are exposed through properties
    internal long connectionTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStatistics"/> class.
    /// </summary>
    /// <param name="duckDBNativeConnection">The native DuckDB connection.</param>
    /// <param name="enableQueryExecutionTracing">Indicates whether query execution tracing is enabled.</param>
    /// <param name="profilingOptions">The profiling options for the connection.</param>
    internal ConnectionStatistics(DuckDBNativeConnection duckDBNativeConnection, bool enableQueryExecutionTracing = false, ProfilingOptions? profilingOptions = default)
        : base(duckDBNativeConnection, profilingOptions)
    {
        this.enableQueryExecutionTracing = enableQueryExecutionTracing;

        // Add or update the mapping for the native connection to this ConnectionStatistics instance
        ByNativeConnection.AddOrUpdate(duckDBNativeConnection, this, (_, _) => this);
    }

    /// <summary>
    /// Enables tracing of query execution for diagnostic or debugging purposes.
    /// </summary>
    /// <remarks>After calling this method, additional diagnostic information about query execution may be collected
    /// or logged. This can assist in troubleshooting or performance analysis. Tracing remains enabled until explicitly
    /// disabled</remarks>
    internal void EnableQueryExecutionTracing()
    {
        this.enableQueryExecutionTracing = true;
    }

    /// <summary>
    /// Disables tracing of query execution for the current instance.
    /// </summary>
    /// <remarks>After calling this method, query execution tracing will no longer be performed until explicitly
    /// re-enabled. This may affect the ability to diagnose or audit query behavior.</remarks>
    internal void DisableQueryExecutionTracing()
    {
        this.enableQueryExecutionTracing = false;
    }

    /// <summary>
    /// Attempts to retrieve a <see cref="ConnectionStatistics"/> associated with the specified <see cref="DuckDBNativeConnection"/> connection.
    /// </summary>
    /// <param name="nativeConn">The native DuckDB connection for which to obtain statistics.</param>
    /// <param name="stats">When this method returns, contains the statistics for the specified connection if found; otherwise, <see
    /// langword="null"/>.</param>
    /// <returns><see langword="true"/> if statistics are found for the specified connection; otherwise, <see
    /// langword="false"/>.</returns>
    internal static bool TryGetFor(DuckDBNativeConnection nativeConn, out ConnectionStatistics? stats)
    {
        // Fast path: if no connections have profiling enabled, skip the lookup entirely
        if (ByNativeConnection.IsEmpty)
        {
            stats = null;
            return false;
        }
        return ByNativeConnection.TryGetValue(nativeConn, out stats);
    }

    /// <summary>
    /// Creates a new <see cref="QueryProfiler"/> for the specified query if query execution tracing is enabled.
    /// </summary>
    /// <param name="queryIdentifier">A pointer that uniquely identifies the query for which the profiler is created.</param>
    /// <param name="statementCount">The number of statements in the query to be profiled. Must be non-negative.</param>
    /// <returns>A new instance of <see cref="QueryProfiler"/> if query execution tracing is enabled; otherwise, <see langword="null"/>.</returns>
    internal QueryProfiler? CreateQueryProfiler(IntPtr queryIdentifier, int statementCount)
    {
        if (!enableQueryExecutionTracing)
        {
            return null;
        }

        var queryProfiler = new QueryProfiler(queryIdentifier, statementCount, duckDBNativeConnection, profilingOptions);
        queryProfilers[queryIdentifier] = queryProfiler;
        return queryProfiler;
    }

    internal void UpdateStatistics()
    {
        // update connection time
        if (endTime >= startTime && long.MaxValue > (endTime - startTime).Ticks)
        {
            connectionTime = (endTime - startTime).Ticks;
        }
        else
        {
            connectionTime = long.MaxValue;
        }
    }

    private IEnumerable<ProfilingQuerySummary> ReadSummaries()
    {
        foreach (var queryProfiler in queryProfilers.Values.OrderBy(qp => qp.StartTime))
        {
            yield return queryProfiler.GetSummary();
        }
    }

    internal ProfilingSummary GetProfilingSummary()
    {
        return new ProfilingSummary(
             startTime,
             endTime,
            TimerUtils.TimerToMilliseconds(connectionTime),
            TimerUtils.TimerToMilliseconds(queryProfilers.Values.Sum(qp => qp.ExecutionTime)),
            queryProfilers.Values.Count,
            [.. ReadSummaries()]);
    }

    public override void Reset()
    {
        executionTime = 0;
        startTimestamp = null;
        connectionTime = 0;
        startTime = default;
        endTime = default;

        queryProfilers.Clear();
    }

    // call on connection close/dispose
    private void RemoveMapping()
    {
        ByNativeConnection.TryRemove(duckDBNativeConnection, out _);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            RemoveMapping();
            isDisposed = true;
        }
    }
}
