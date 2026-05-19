using DuckDB.NET.Data.Common;
using System.Collections.Concurrent;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class ConnectionStatistics : IDisposable
    {

        // internal values that are not exposed through properties
        private static readonly ConcurrentDictionary<DuckDBNativeConnection, ConnectionStatistics> ByNativeConnection
    = new(ReferenceEqualityComparer.Instance);


        internal long closeTimestamp;
        internal long openTimestamp;
        internal long? startExecutionTimestamp;
        private bool enableQueryExecutionTracing;
        private readonly DuckDBNativeConnection duckDBNativeConnection;
        private readonly Dictionary<IntPtr, QueryProfiler> queryProfilers = [];
        private bool isDisposed = false;

        // internal values that are exposed through properties
        internal long executionTime;
        internal long connectionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionStatistics"/> class.
        /// </summary>
        /// <param name="duckDBNativeConnection">The native DuckDB connection.</param>
        /// <param name="enableQueryExecutionTracing">Indicates whether query execution tracing is enabled.</param>
        internal ConnectionStatistics(DuckDBNativeConnection duckDBNativeConnection, bool enableQueryExecutionTracing = false)
        {
            this.enableQueryExecutionTracing = enableQueryExecutionTracing;
            this.duckDBNativeConnection = duckDBNativeConnection;

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
            => ByNativeConnection.TryGetValue(nativeConn, out stats);

        /// <summary>
        /// Creates a new <see cref="QueryProfilerTracer"/> for the specified query if query execution tracing is enabled.
        /// </summary>
        /// <param name="queryIdentifier">A pointer that uniquely identifies the query for which the profiler tracer is created.</param>
        /// <param name="statementCount">The number of statements in the query to be profiled. Must be non-negative.</param>
        /// <returns>A new instance of <see cref="QueryProfilerTracer"/> if query execution tracing is enabled; otherwise, <see langword="null"/>.</returns>
        internal QueryProfilerTracer? CreateQueryProfilerTracer(IntPtr queryIdentifier, int statementCount)
        {
            if (!enableQueryExecutionTracing)
            {
                return null;
            }

            var queryProfiler = new QueryProfiler(queryIdentifier, statementCount, duckDBNativeConnection);
            var tracer = new QueryProfilerTracer(queryProfiler, duckDBNativeConnection);

            queryProfilers[queryIdentifier] = queryProfiler;

            return tracer;
        }

        internal void UpdateStatistics()
        {
            // update connection time
            if (closeTimestamp >= openTimestamp && long.MaxValue > closeTimestamp - openTimestamp)
            {
                connectionTime = closeTimestamp - openTimestamp;
            }
            else
            {
                connectionTime = long.MaxValue;
            }
        }

        private IEnumerable<ProfilingQuerySummary> ReadSummaries()
        {
            foreach (var queryProfiler in queryProfilers.Values)
            {
                yield return queryProfiler.GetQuerySummary();
            }
        }

        internal ProfilingSummary GetProfilingSummary()
        {
            return new ProfilingSummary(
                startExecutionTime,
                endExecutionTime,
                TimerUtils.TimerToMilliseconds(connectionTime),
                TimerUtils.TimerToMilliseconds(executionTime),
                queryProfilers.Values.Count,
                [.. ReadSummaries()]);
        }

        internal void Reset()
        {
            executionTime = 0;
            startExecutionTimestamp = null;
            connectionTime = 0;
            startExecutionTime = default;
            endExecutionTime = default;

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
}
