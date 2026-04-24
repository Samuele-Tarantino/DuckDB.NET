using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Connection
{
    internal sealed class StatementExecutionTracer : IDisposable
    {
        private readonly QueryExecutionTimer timer;
        bool isEnabled = false;

        internal StatementExecutionTracer(QueryExecutionTimer timer)
        {
            this.timer = timer;
        }

        internal void StartTimer()
        {
            timer.StartTimer();
        }

        internal void StopTimer()
        {
            timer.StopTimer();
        }

        internal bool IsEnabled => isEnabled;

        internal void ReadMetrics(DuckDBNativeConnection connection)
        {
            var profile = new ProfilingInfo(connection);

            if (profile.TryPrepare())
            {
                var curMetrics = profile.GetMetrics();
                metrics[index] = curMetrics;
            }
        }

        public void Dispose()
        {
            timer.StopTimer();
        }
    }
}
