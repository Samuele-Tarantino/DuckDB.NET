namespace DuckDB.NET.Data.Profiling
{
    public readonly struct ProfilingOptions
    {
        /// <summary>
        /// The profiling output format to use when generating profiling results.
        /// </summary>
        public DuckDBProfilingFormat Format { get; init; }

        /// <summary>
        /// The profiling mode used for DuckDB query execution.
        /// </summary>
        public DuckDBProfilingMode Mode { get; init; }

        /// <summary>
        /// The profiling coverage settings used for DuckDB query analysis.
        /// </summary>
        public DuckDBProfilingCoverage Coverage { get; init; }

        /// <summary>
        /// The output directory path where generated files will be placed.
        /// </summary>
        public string OutputPath { get; init; }

        /// <summary>
        /// The collection of enabled metrics for profiling.
        /// </summary>
        public DuckDBMetricTypeCollection EnabledMetrics { get; init; }

        /// <summary>
        /// The threshold (in milliseconds) used for metrics acquisition. 
        /// </summary>
        /// <remarks>Execution times below this threshold may not trigger metrics collection.</remarks>
        public int MetricsThreshold { get; init; }
    }
}
