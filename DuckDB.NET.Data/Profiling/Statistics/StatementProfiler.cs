using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling.Statistics.Summary;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class StatementProfiler(int queryIndex, DuckDBNativeConnection connection)
    : ExecutionProfiler(connection)
    {
        private Dictionary<string, string> rawMetrics = new();

        internal long? startExecutionTimestamp;
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;


        internal long ExecutionTime => TimerUtils.TimerToMilliseconds(executionTime);
        internal DateTimeOffset StartTime => startExecutionTime;
        internal DateTimeOffset EndTime => endExecutionTime;

        internal int QueryIndex => queryIndex;
        internal ProfilingInfoMetrics Info => ProfilingInfoMetrics.FromRawMetrics(rawMetrics);


        internal ProfilingStatementSummary GetSummary()
        {
            return new ProfilingStatementSummary(
                StartTime: startExecutionTime,
                EndTime: endExecutionTime,
                ExecutionTimeMilliseconds: TimerUtils.TimerToMilliseconds(executionTime),
                Order: queryIndex,
                Metrics: Info);
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

        public override void AcquireMetrics()
        {
            var profile = new ProfilingInfo(duckDBNativeConnection);

            if (profile.TryPrepare())
            {
                rawMetrics = profile.GetRawMetrics();
            }
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
