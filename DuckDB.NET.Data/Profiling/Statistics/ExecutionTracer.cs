namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal abstract class ExecutionTracer : IExecutionTracerBase, IDisposable
    {
        protected readonly ExecutionStatisticsBase statistics;
        private bool disposed;

        internal ExecutionTracer(ExecutionStatisticsBase statistics)
        {
            this.statistics = statistics;
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

        void IExecutionTracerBase.StartTimer()
        {
            StartTimer();
        }

        void IExecutionTracerBase.StopTimer()
        {
            StopTimer();
        }

        void IExecutionTracerBase.SetState(DuckDBState state)
        {
            SetState(state);
        }

        void IExecutionTracerBase.SetState(DuckDBState state, string message)
        {
            SetState(state, message);
        }

        void IExecutionTracerBase.SetState(DuckDBState state, DuckDBErrorType errorType, string message)
        {
            SetState(state, errorType, message);
        }

        DuckDBState IExecutionTracerBase.State => State;
    }
}