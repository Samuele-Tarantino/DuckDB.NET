namespace DuckDB.NET.Data.Profiling.Statistics
{
    // Single interface — replaces both IExecutionTracerBase and IExecutionProfiler
    internal interface IExecutionProfiler
    {
        void StartTimer();
        void StopTimer();
        void SetState(DuckDBState state);
        void SetState(DuckDBState state, string message);
        void SetState(DuckDBState state, DuckDBErrorType errorType, string message);
        void AcquireMetrics();
        DuckDBState State { get; }
    }
}