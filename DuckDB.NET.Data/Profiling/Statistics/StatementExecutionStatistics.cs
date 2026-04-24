using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class StatementExecutionStatistics: ExecutionStatistics
    {
        // internal values that are not exposed through properties
        internal long? startExecutionTimestamp;

        // internal values that are exposed through properties
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        internal Dictionary<int, IDictionary<string, object>> metrics = [];
        private readonly DuckDBPreparedStatement preparedStatement;
        private readonly int queryIndex;

        internal StatementExecutionStatistics(DuckDBPreparedStatement preparedStatement, int queryIndex, DuckDBNativeConnection duckDBNativeConnection): base(duckDBNativeConnection)
        {
            this.preparedStatement = preparedStatement;
            this.queryIndex = queryIndex;
        }

        internal override void StartTimer()
        {
            if (!startExecutionTimestamp.HasValue)
            {
                startExecutionTimestamp = TimerUtils.TimerCurrent();
                startExecutionTime = TimerUtils.Now();
            }
        }

        internal override void StopTimer()
        {
            ReleaseAndUpdateExecutionTimer();
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

        internal override void AcquireMetrics()
        {
            var profile = new ProfilingInfo(duckDBNativeConnection);

            if (profile.TryPrepare())
            {
                var curMetrics = profile.GetMetrics();
                //metrics[index] = curMetrics;
            }
        }

        internal IDictionary GetDictionary()
        {
            //const int Count = 18;
            var dictionary = new Dictionary<string, object>(/*Count*/)
            {
                { "StartTime", startExecutionTime  },
                { "EndTime", endExecutionTime },
                { "ExecutionTime", TimerUtils.TimerToMilliseconds(executionTime) },
                { "StatementIndex", queryIndex }
            };
            //Debug.Assert(dictionary.Count == Count);
            return dictionary;
        }

        internal override void Reset()
        {
            executionTime = 0;
            startExecutionTimestamp = null;
            startExecutionTime = default;
            endExecutionTime = default;
        }
    }
}
