namespace DuckDB.NET.Data.Profiling
{
    public readonly struct ProfilingOptions
    {
        public DuckDBProfilingFormat Format { get; init; }
        public DuckDBProfilingMode Mode { get; init; }
        public DuckDBProfilingCoverage Coverage { get; init; }
        public string OutputPath { get; init; }

        public DuckDBMetricTypeCollection EnabledMetrics { get; init; }
    }
}
