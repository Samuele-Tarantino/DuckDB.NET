using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class SqlStatistics
    {

        // internal values that are not exposed through properties
        internal long closeTimestamp;
        internal long openTimestamp;
        internal long? startExecutionTimestamp;
        private readonly bool enableQueryExecutionTracing;
        private readonly DuckDBNativeConnection duckDBNativeConnection;
        private readonly Dictionary<IntPtr, QueryExecutionTracer> queryExecutionTracers = [];
        private readonly Dictionary<IntPtr, List<QueryExecutionStatistics>> queryExecutionStatistics = [];

        // internal values that are exposed through properties
        internal long executionTime;
        internal long connectionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        internal Dictionary<int, IDictionary<string, object>> metrics = [];


        internal SqlStatistics(DuckDBNativeConnection duckDBNativeConnection, bool enableQueryExecutionTracing = false)
        {
            this.enableQueryExecutionTracing = enableQueryExecutionTracing;
            this.duckDBNativeConnection = duckDBNativeConnection;
        }

        internal QueryExecutionTracer? CreateQueryTracer(IntPtr queryIdentifier, int statementCount)
        {
            if (!enableQueryExecutionTracing)
            {
                return null;
            }

            var executionStatistics = new QueryExecutionStatistics(queryIdentifier, statementCount, duckDBNativeConnection);
            var tracer = new QueryExecutionTracer(executionStatistics, duckDBNativeConnection);
            queryExecutionTracers[queryIdentifier] = tracer;

            // When a new tracer is created for a query, we also create a new list to hold the execution statistics for that query.
            if (!queryExecutionStatistics.TryGetValue(queryIdentifier, out var executionStatisticsList))
            {
                executionStatisticsList = [];
                queryExecutionStatistics[queryIdentifier] = executionStatisticsList;
            }
            executionStatisticsList.Add(executionStatistics);

            return tracer;
        }

        internal QueryExecutionTracer? GetQueryTracer(IntPtr queryIdentifier)
        {
            if (!enableQueryExecutionTracing)
            {
                return null;
            }
            queryExecutionTracers.TryGetValue(queryIdentifier, out var tracer);
            return tracer;
        }

        //internal QueryExecutionTracer? CreateExecutionTracer(IntPtr queryIdentifier, DuckDBPreparedStatement preparedStatement, int queryIndex)
        //{
        //    if (!enableQueryExecutionTracing)
        //    {
        //        return null;
        //    }

        //    if (!queryExecutionTimers.TryGetValue(queryIdentifier, out var queryExecutionTimer))
        //    {
        //        queryExecutionTimer = new QueryExecutionTimer(preparedStatement, queryIndex);
        //        queryExecutionTimers[queryIdentifier] = queryExecutionTimer;
        //    }

        //    return new QueryExecutionTracer(queryExecutionTimer, queryIdentifier);
        //}

        //internal void StartTimer()
        //{
        //    if (!startExecutionTimestamp.HasValue)
        //    {
        //        startExecutionTimestamp = TimerUtils.TimerCurrent();
        //        startExecutionTime = TimerUtils.Now();
        //    }
        //}

        //internal void StopTimer()
        //{
        //    ReleaseAndUpdateExecutionTimer();
        //}

        //internal void ReleaseAndUpdateExecutionTimer()
        //{
        //    if (startExecutionTimestamp.HasValue)
        //    {
        //        uint elapsed = TimerUtils.CalculateTickCountElapsed(startExecutionTimestamp.Value, TimerUtils.TimerCurrent());
        //        executionTime += elapsed;
        //        endExecutionTime = startExecutionTime.AddTicks(elapsed);

        //        startExecutionTimestamp = null;
        //    }
        //}

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

        internal void ReadMetrics(DuckDBNativeConnection connection, int index)
        {
            var profile = new ProfilingInfo(connection);

            if (profile.TryPrepare())
            {
                var curMetrics = profile.GetMetrics();
                metrics[index] = curMetrics;
            }
        }

        internal IDictionary GetDictionary()
        {
            const int Count = 18;
            var dictionary = new Dictionary<string, object>(Count)
            {
                { "StartTime", startExecutionTime  },
                { "EndTime", endExecutionTime },
                { "ConnectionTime", TimerUtils.TimerToMilliseconds(connectionTime) },
                { "ExecutionTime", TimerUtils.TimerToMilliseconds(executionTime) },
                { "MetricsCount", metrics.Count  },
                { "Metrics", metrics }
            };
            Debug.Assert(dictionary.Count == Count);
            return dictionary;
        }

        internal void Reset()
        {
            executionTime = 0;
            startExecutionTimestamp = null;
            connectionTime = 0;
            startExecutionTime = default;
            endExecutionTime = default;
        }
    }
}
