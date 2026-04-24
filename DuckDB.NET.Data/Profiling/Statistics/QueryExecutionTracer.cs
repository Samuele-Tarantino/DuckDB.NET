using DuckDB.NET.Data.PreparedStatement;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal sealed class QueryExecutionTracer : ExecutionTracer
    {
        private readonly Dictionary<DuckDBPreparedStatement, StatementExecutionTracer> statementTracers = [];
        private readonly QueryExecutionStatistics queryExecutionStatistics;
        private readonly DuckDBNativeConnection duckDBNativeConnection;

        internal QueryExecutionTracer(QueryExecutionStatistics queryExecutionStatistics, DuckDBNativeConnection duckDBNativeConnection): base(queryExecutionStatistics)
        {
            this.queryExecutionStatistics = queryExecutionStatistics;
            this.duckDBNativeConnection = duckDBNativeConnection;
        }

        internal void PrepareStatementTracer(DuckDBPreparedStatement preparedStatement, int statementIndex)
        {
            if (!statementTracers.ContainsKey(preparedStatement))
            {
                var singleStatementStatistics = new StatementExecutionStatistics(preparedStatement, statementIndex, duckDBNativeConnection);
                statementTracers[preparedStatement] = new StatementExecutionTracer(singleStatementStatistics);

                queryExecutionStatistics.TryAddExecutionStatistics(singleStatementStatistics, statementIndex);
            }
        }

        internal StatementExecutionTracer? GetStatementTracer(DuckDBPreparedStatement preparedStatement)
        {
            if (!statementTracers.TryGetValue(preparedStatement, out var tracer))
            {
                return null;
            }
            return tracer;
        }
    }
}
