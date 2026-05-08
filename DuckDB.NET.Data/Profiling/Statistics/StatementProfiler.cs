using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class StatementProfiler: ExecutionStatistics
    {
        // internal values that are not exposed through properties
        internal long? startExecutionTimestamp;

        // internal values that are exposed through properties
        internal long executionTime;
        internal DateTimeOffset startExecutionTime;
        internal DateTimeOffset endExecutionTime;
        private ProfilingInfoMetrics info;
        private readonly DuckDBPreparedStatement preparedStatement;
        private readonly int queryIndex;

        internal StatementProfiler(DuckDBPreparedStatement preparedStatement, int queryIndex, DuckDBNativeConnection duckDBNativeConnection): base(duckDBNativeConnection)
        {
            this.preparedStatement = preparedStatement;
            this.queryIndex = queryIndex;
        }

        public ProfilingInfoMetrics Info => info;

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

        internal override void AcquireProfilingInfo()
        {
            var profile = new ProfilingInfo(duckDBNativeConnection);

            if (profile.TryPrepare())
            {
                info = profile.GetMetrics();
            }
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
