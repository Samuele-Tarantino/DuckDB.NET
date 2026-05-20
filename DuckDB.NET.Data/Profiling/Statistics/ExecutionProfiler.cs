namespace DuckDB.NET.Data.Profiling.Statistics
{
    // Single base class — replaces ExecutionStatisticsBase + ExecutionStatistics + ExecutionTracer
    internal abstract class ExecutionProfiler(DuckDBNativeConnection duckDBNativeConnection) : IExecutionProfiler
    {
        protected readonly DuckDBNativeConnection duckDBNativeConnection = duckDBNativeConnection;
        protected DuckDBState state;
        protected string? errorMessage;
        private bool disposed;

        public abstract void StartTimer();
        public abstract void StopTimer();
        public virtual void AcquireMetrics() { }
        public abstract void Reset();

        public DuckDBState State => state;
        public string ErrorMessage => errorMessage ?? string.Empty;

        public void SetState(DuckDBState state) => this.state = state;

        public void SetState(DuckDBState state, string message)
        {
            this.state = state;
            this.errorMessage = message;
        }

        public void SetState(DuckDBState state, DuckDBErrorType errorType, string message)
        {
            this.state = state;
            this.errorMessage = $"{errorType} error: {message}";
        }
    }
}