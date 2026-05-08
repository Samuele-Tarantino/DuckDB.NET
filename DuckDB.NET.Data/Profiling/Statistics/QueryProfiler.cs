using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryProfiler : ExecutionStatistics
    {
        // internal values that are not exposed through properties
        internal long? startExecutionTimestamp;
        private readonly StatementProfiler[] statementProfilers;

        // internal values that are exposed through properties
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        internal Dictionary<int, IDictionary<string, object>> metrics = [];
        private readonly IntPtr queryIdentifier;
        private readonly int statementCount;

        internal QueryProfiler(IntPtr queryIdentifier, int statementCount, DuckDBNativeConnection duckDBNativeConnection): base(duckDBNativeConnection)
        {
            this.statementCount = statementCount;
            this.statementProfilers = new StatementProfiler[statementCount];
        }

        internal IntPtr QueryIdentifier => queryIdentifier;

        internal bool RegisterStatementProfiler(StatementProfiler statistics, int queryIndex)
        {
            if (statementProfilers[queryIndex] == null)
            {
                statementProfilers[queryIndex] = statistics;
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
