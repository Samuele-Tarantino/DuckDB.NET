using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryExecutionStatistics : ExecutionStatistics
    {
        // internal values that are not exposed through properties
        internal long? startExecutionTimestamp;
        private readonly StatementExecutionStatistics[] executionStatistics;

        // internal values that are exposed through properties
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        internal Dictionary<int, IDictionary<string, object>> metrics = [];
        private readonly IntPtr queryIdentifier;
        private readonly int statementCount;

        internal QueryExecutionStatistics(IntPtr queryIdentifier, int statementCount, DuckDBNativeConnection duckDBNativeConnection): base(duckDBNativeConnection)
        {
            this.statementCount = statementCount;
            this.executionStatistics = new StatementExecutionStatistics[statementCount];
        }

        internal IntPtr QueryIdentifier => queryIdentifier;

        internal bool TryAddExecutionStatistics(StatementExecutionStatistics statistics, int queryIndex)
        {
            if (executionStatistics[queryIndex] == null)
            {
                executionStatistics[queryIndex] = statistics;
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
            //const int Count = 18;
            var dictionary = new Dictionary<string, object>(/*Count*/)
            {
                { "StartTime", startExecutionTime  },
                { "EndTime", endExecutionTime },
                { "ExecutionTime", TimerUtils.TimerToMilliseconds(executionTime) },
                { "StatementCount", statementCount }
            };
            //Debug.Assert(dictionary.Count == Count);
            return dictionary;
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

        internal override void AcquireMetrics()
        {
            var profile = new ProfilingInfo(duckDBNativeConnection);

            if (profile.TryPrepare())
            {
                var curMetrics = profile.GetMetrics();
                //metrics[index] = curMetrics;
            }
        }

        internal override void SetState(DuckDBState state, string message)
        {
            this.state = state;
            this.errorMessage = message;
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
