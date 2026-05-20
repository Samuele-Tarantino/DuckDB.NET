using DuckDB.NET.Data.Common;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryProfiler(IntPtr queryIdentifier, int statementCount, DuckDBNativeConnection connection)
    : ExecutionProfiler(connection)
    {
        private readonly StatementProfiler[] statementProfilers = new StatementProfiler[statementCount];

        internal long? startExecutionTimestamp;
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;


        internal long ExecutionTime => TimerUtils.TimerToMilliseconds(executionTime);
        internal DateTimeOffset StartTime => startExecutionTime;
        internal DateTimeOffset EndTime => endExecutionTime;
        internal int StatementCount => statementCount;

        internal IntPtr QueryIdentifier => queryIdentifier;

        internal StatementProfiler CreateStatementProfiler(int index)
        {
            var profiler = new StatementProfiler(index, duckDBNativeConnection);
            statementProfilers[index] = profiler;
            return profiler;
        }

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
                long elapsed = TimerUtils.CalculateTickCountElapsed(startExecutionTimestamp.Value, TimerUtils.TimerCurrent());
                executionTime += elapsed;
                endExecutionTime = startExecutionTime.AddTicks(elapsed);

                startExecutionTimestamp = null;
            }
        }

        private IEnumerable<ProfilingStatementSummary> GetStatementInfo()
        {
            foreach (var statementProfiler in statementProfilers)
            {
                yield return statementProfiler.GetSummary();
            }
        }

        internal ProfilingQuerySummary GetSummary()
        {
            return new ProfilingQuerySummary(
                 startExecutionTime,
                 endExecutionTime,
                 TimerUtils.TimerToMilliseconds(executionTime),
                 statementCount,
                 [.. GetStatementInfo()]);
        }

        public override void StartTimer()
        {
            if (!startExecutionTimestamp.HasValue)
            {
                startExecutionTimestamp = TimerUtils.TimerCurrent();
                startExecutionTime = new DateTimeOffset(startExecutionTimestamp.Value, TimeSpan.Zero);
            }
        }

        public override void StopTimer()
        {
            ReleaseAndUpdateExecutionTimer();
        }

        public override void Reset()
        {
            executionTime = 0;
            startExecutionTimestamp = null;
            startExecutionTime = default;
            endExecutionTime = default;
        }
    }
}
