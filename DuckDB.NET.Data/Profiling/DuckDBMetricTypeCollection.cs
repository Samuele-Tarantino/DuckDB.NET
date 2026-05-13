using System.Collections.Generic;

namespace DuckDB.NET.Data.Profiling
{
    public class DuckDBMetricTypeCollection : List<DuckDBMetricType>
    {
        public DuckDBMetricTypeCollection() { }

        public DuckDBMetricTypeCollection(IEnumerable<DuckDBMetricType> collection) : base(collection) { }
    }
}
