using DuckDB.NET.Data.Common;
using System.Diagnostics;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class StatementExecutionTracer : ExecutionTracer
    {
        internal StatementExecutionTracer(StatementExecutionStatistics statistics): base(statistics) { }

    }
}