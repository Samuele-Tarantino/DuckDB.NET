namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryProfilerTracer : ExecutionTracer
    {
        private readonly Dictionary<DuckDBPreparedStatement, StatementProfilerTracer> statementTracers = [];
        private readonly QueryProfiler queryProfiler;
        private readonly DuckDBNativeConnection duckDBNativeConnection;

        internal QueryProfilerTracer(QueryProfiler queryProfiler, DuckDBNativeConnection duckDBNativeConnection) : base(queryProfiler)
        {
            this.queryProfiler = queryProfiler;
            this.duckDBNativeConnection = duckDBNativeConnection;
        }

        internal StatementProfilerTracer CreateStatementProfilerTracer(DuckDBPreparedStatement preparedStatement, int statementIndex)
        {
            if (statementTracers.ContainsKey(preparedStatement))
            {
                throw new InvalidOperationException("Statement profiler tracer already exists for the given prepared statement.");
            }

            var statementProfiler = new StatementProfiler(statementIndex, duckDBNativeConnection);
            statementTracers[preparedStatement] = new StatementProfilerTracer(statementProfiler);

            queryProfiler.RegisterStatementProfiler(statementProfiler, statementIndex);

            return statementTracers[preparedStatement];
        }

        internal StatementProfilerTracer GetOrCreateStatementProfilerTracer(DuckDBPreparedStatement preparedStatement, int? statementIndex = null)
        {
            if (!statementTracers.TryGetValue(preparedStatement, out var tracer))
            {
                var index = statementIndex ?? throw new ArgumentNullException(nameof(statementIndex));
                return CreateStatementProfilerTracer(preparedStatement, index);
            }

            return tracer;
        }
    }
}
