using DuckDB.NET.Data.Common;
using System.Diagnostics;
using System.Threading;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class StatementProfilerTracer : ExecutionTracer
    {
        private static readonly AsyncLocal<StatementProfilerTracer?> currentTracer = new();

        internal StatementProfilerTracer(StatementProfiler statistics): base(statistics) { }

        /// <summary>
        /// Gets or sets the current <see cref="StatementProfilerTracer"/> for the current
        /// execution context. This flows across async calls.
        /// </summary>
        public static StatementProfilerTracer? Current
        {
            get => currentTracer.Value;
            set => currentTracer.Value = value;
        }
    }
}