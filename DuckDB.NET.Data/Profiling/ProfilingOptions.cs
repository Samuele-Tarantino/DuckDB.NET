using System.Collections.Generic;

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
        /// <summary>Legacy way to select metrics. Mapped to the metric names of DuckDB 2.0; prefer <see cref="TrackedMetrics"/>.</summary>
        [Obsolete("Use TrackedMetrics with the metric names of DuckDBMetricNames.")]
        public DuckDBMetricTypeCollection EnabledMetrics { get; init; }

        /// <summary>
        /// The DuckDB 2.0 metric names or globs to track, e.g. <see cref="DuckDBMetricNames.QueryTotalTime"/> or <c>"query.*"</c>.
        /// Combined with <see cref="EnabledMetrics"/> when both are set; when neither is set a default set is tracked.
        /// </summary>
        public IReadOnlyCollection<string> TrackedMetrics { get; init; }

        /// <summary>
        /// The threshold (in milliseconds) used for metrics acquisition.
        /// </summary>
        /// <remarks>Execution times below this threshold may not trigger metrics collection.</remarks>
        public int MetricsThreshold { get; init; }
    }
}
