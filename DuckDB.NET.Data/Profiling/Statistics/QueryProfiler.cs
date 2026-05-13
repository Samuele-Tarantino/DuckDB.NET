using DuckDB.NET.Data.Common;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryProfiler : ExecutionStatisticsBase
    {
        // internal values that are not exposed through properties
        internal long? startExecutionTimestamp;
        private readonly StatementProfiler[] statementProfilers;

        // internal values that are exposed through properties
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        private Dictionary<int, ProfilingInfoMetrics> infos = [];
        private readonly IntPtr queryIdentifier;
        private readonly int statementCount;

        internal QueryProfiler(IntPtr queryIdentifier, int statementCount, DuckDBNativeConnection duckDBNativeConnection) : base(duckDBNativeConnection)
        {
            this.statementCount = statementCount;
            this.statementProfilers = new StatementProfiler[statementCount];
        }

        internal IntPtr QueryIdentifier => queryIdentifier;

        internal int StatementCount => statementCount;

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

        private IEnumerable<ProfilingInfoMetrics> GetStatementInfo()
        {
            foreach (var statementProfiler in statementProfilers)
            {
                yield return statementProfiler?.Info ?? [];
            }
        }

        internal ProfilingQuerySummary GetQuerySummary()
        {
            return new ProfilingQuerySummary(
                 startExecutionTime,
                 endExecutionTime,
                 TimerUtils.TimerToMilliseconds(executionTime),
                 statementCount,
                 [.. GetStatementInfo()]);
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
