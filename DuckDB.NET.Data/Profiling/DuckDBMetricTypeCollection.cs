using System.Collections.Generic;

namespace DuckDB.NET.Data.Profiling
{
    [Obsolete("Use ProfilingOptions.TrackedMetrics with the metric names of DuckDBMetricNames.")]
    public class DuckDBMetricTypeCollection : List<DuckDBMetricType>
    {
        public DuckDBMetricTypeCollection() { }

        public DuckDBMetricTypeCollection(IEnumerable<DuckDBMetricType> collection) : base(collection) { }
    }
}
