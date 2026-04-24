namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal abstract class ExecutionTracer : IExecutionTracer, IDisposable
    {
        protected readonly ExecutionStatistics statistics;
        private bool disposed;

        internal ExecutionTracer(ExecutionStatistics statistics)
        {
            this.statistics = statistics;
        }

        internal virtual void AcquireMetrics()
        {
            statistics.AcquireMetrics();
        }

        internal virtual void StartTimer()
        {
            statistics.StartTimer();
        }

        internal virtual void StopTimer()
        {
            statistics.StopTimer();
        }

        internal virtual void Dispose()
        {
            statistics.StopTimer();
        }

        internal virtual void SetState(DuckDBState state)
        {
            statistics.SetState(state);
        }

        internal virtual void SetState(DuckDBState state, string message)
        {
            statistics.SetState(state, message);
        }

        internal virtual void SetState(DuckDBState state, DuckDBErrorType errorType, string message)
        {
            statistics.SetState(state, errorType, message);
        }

        internal virtual DuckDBState State => statistics.State;

        void IDisposable.Dispose()
        {
            if (!disposed)
            {
                Dispose();
                disposed = true;
            }
        }

        void IExecutionTracer.AcquireMetrics()
        {
            AcquireMetrics();
        }

        void IExecutionTracer.StartTimer()
        {
            StartTimer();
        }

        void IExecutionTracer.StopTimer()
        {
            StopTimer();
        }

        void IExecutionTracer.SetState(DuckDBState state)
        {
            SetState(state);
        }

        void IExecutionTracer.SetState(DuckDBState state, string message)
        {
            SetState(state, message);
        }

        void IExecutionTracer.SetState(DuckDBState state, DuckDBErrorType errorType, string message)
        {
            SetState(state, errorType, message);
        }

        DuckDBState IExecutionTracer.State => State;
    }
}