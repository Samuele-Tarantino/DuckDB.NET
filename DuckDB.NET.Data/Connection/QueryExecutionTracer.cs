using DuckDB.NET.Data.PreparedStatement;
using System.Net.NetworkInformation;

namespace DuckDB.NET.Data.Connection
{
    internal sealed class QueryExecutionTracer : IDisposable
    {
        private readonly QueryExecutionTimer timer;
        private readonly Dictionary<DuckDBPreparedStatement, QueryExecutionTimer> statementTimers = [];
        private readonly nint queryIdentifier;
        private readonly int statementCount;

        internal QueryExecutionTracer(IntPtr queryIdentifier, int statementCount)
        {
            this.queryIdentifier = queryIdentifier;
            this.statementCount = statementCount;
        }

        internal QueryExecutionTracer(QueryExecutionTimer timer, IntPtr queryIdentifier)
        {
            this.timer = timer;
        }

        internal StatementExecutionTracer CreateStatementTracer(DuckDBPreparedStatement preparedStatement, int queryIndex)
        {
            if (!statementTimers.TryGetValue(preparedStatement, out var statementTimer))
            {
                statementTimer = new QueryExecutionTimer(preparedStatement, queryIndex);
                statementTimers[preparedStatement] = statementTimer;
            }

            return new StatementExecutionTracer(statementTimer);
        }

        //internal void StartTimer()
        //{
        //    timer.StartTimer();
        //}

        //internal void StopTimer()
        //{
        //    timer.StopTimer();
        //}

        internal void AcquireMetrics(DuckDBNativeConnection connection)
        {
            //var profile = new ProfilingInfo(connection);

            //if (profile.TryPrepare())
            //{
            //    var curMetrics = profile.GetMetrics();
            //    metrics[index] = curMetrics;
            //}
        }

        public void Dispose()
        {
            timer.StopTimer();
        }
    }
}
