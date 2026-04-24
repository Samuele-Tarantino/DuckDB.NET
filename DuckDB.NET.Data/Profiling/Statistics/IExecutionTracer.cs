namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal interface IExecutionTracer : IDisposable
    {
        void AcquireMetrics();
        void StartTimer();
        void StopTimer();
        void SetState(DuckDBState state);
        void SetState(DuckDBState state, string message);
        void SetState(DuckDBState state, DuckDBErrorType errorType, string message);
        DuckDBState State { get; }
    }
}