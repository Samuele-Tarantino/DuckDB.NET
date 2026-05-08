namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal interface IExecutionTracer : IExecutionTracerBase
    {
        void AcquireMetrics();
    }
}