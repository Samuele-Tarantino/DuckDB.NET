using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Connection
{
    internal sealed class SqlStatistics
    {

        // internal values that are not exposed through properties
        internal long closeTimestamp;
        internal long openTimestamp;
        internal long? startExecutionTimestamp;

        // internal values that are exposed through properties
        internal long executionTime;
        internal long connectionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        internal Dictionary<int, IDictionary<string, object>> metrics = [];

        internal SqlStatistics()
        {
        }

        internal static SqlStatistics StartTimer(SqlStatistics statistics)
        {
            if (statistics != null && !statistics.RequestExecutionTimer())
            {
                // we're re-entrant -- don't bother.
                statistics = null;
            }
            return statistics;
        }

        internal static void StopTimer(SqlStatistics statistics)
        {
            if (statistics != null)
            {
                statistics.ReleaseAndUpdateExecutionTimer();
            }
        }

        internal bool RequestExecutionTimer()
        {
            if (!startExecutionTimestamp.HasValue)
            {
                startExecutionTimestamp = TimerUtils.TimerCurrent();
                startExecutionTime = TimerUtils.Now();
                return true;
            }
            return false;
        }

        internal void ReleaseAndUpdateExecutionTimer()
        {
            if (startExecutionTimestamp.HasValue)
            {
                uint elapsed = TimerUtils.CalculateTickCountElapsed(startExecutionTimestamp.Value, TimerUtils.TimerCurrent());
                executionTime += elapsed;
                endExecutionTime = startExecutionTime.AddTicks(elapsed);

                startExecutionTimestamp = null;
            }
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
