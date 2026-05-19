using DuckDB.NET.Data.Connection;
using DuckDB.NET.Data.Profiling.Statistics;
using System.Linq;

namespace DuckDB.NET.Data.PreparedStatement;

internal sealed class PreparedStatement : IDisposable
{
    private readonly DuckDBPreparedStatement statement;

    private PreparedStatement(DuckDBPreparedStatement statement)
    {
        this.statement = statement;
    }

    public static IEnumerable<DuckDBResult> PrepareMultiple(DuckDBNativeConnection connection, string query, DuckDBParameterCollection parameters, bool useStreamingMode)
    {
        var statementCount = NativeMethods.ExtractStatements.DuckDBExtractStatements(connection, query, out var extractedStatements);

        using (extractedStatements)
        {
            _ = ConnectionStatistics.TryGetFor(connection, out var stats);
            // Initialize the query tracer for the entire batch of statements. The tracer will be responsible for tracking the execution of all statements within this batch.
            using var queryTracer = stats?.CreateQueryProfilerTracer(extractedStatements.ToHandle(), statementCount);
            queryTracer?.StartTimer();

            if (statementCount <= 0)
            {
                var error = NativeMethods.ExtractStatements.DuckDBExtractStatementsError(extractedStatements);

                queryTracer?.SetState(DuckDBState.Error, DuckDBErrorType.Parser, error);

                throw new DuckDBException(error);
            }

            for (int index = 0; index < statementCount; index++)
            {
                var status = NativeMethods.ExtractStatements.DuckDBPrepareExtractedStatement(connection, extractedStatements, index, out var statement);

                var statementTracer = queryTracer?.CreateStatementProfilerTracer(statement, index);
                StatementProfilerTracer.Current = statementTracer;

                if (status.IsSuccess())
                {
                    using var preparedStatement = new PreparedStatement(statement);
                    var result = preparedStatement.Execute(parameters, useStreamingMode, connection);

                    // Stop the query tracer after the last statement has been executed.
                    // This ensures that the total execution time for the entire batch of statements is accurately captured 
                    // and not after the data retrieval of the last statement.
                    if (index == statementCount - 1)
                    {
                        queryTracer?.StopTimer();
                    }

                    yield return result;
                }
                else
                {
                    var errorMessage = NativeMethods.PreparedStatements.DuckDBPrepareError(statement);

                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessage = "DuckDBQuery failed";
                    }

                    // Initialize the statement tracer for the current statement. This allows for detailed tracing of each individual statement within the batch
                    using (statementTracer)
                    {
                        statementTracer?.SetState(status, errorMessage);
                    }
                    StatementProfilerTracer.Current = null;

                    throw new DuckDBException(errorMessage, UdfExceptionStore.Retrieve(connection));
                }
            }
        }
    }

    private DuckDBResult Execute(DuckDBParameterCollection parameterCollection, bool useStreamingMode, DuckDBNativeConnection connection)
    {
        // Tracing is discriminated by query identifier, which is a combination of the query text and the index of the statement in the case of multiple statements.
        // This allows for more granular tracing of individual statements within a batch.
        using var statementTracer = StatementProfilerTracer.Current;
        statementTracer?.StartTimer();

        BindParameters(statement, parameterCollection);

        var status = useStreamingMode
            ? NativeMethods.PreparedStatements.DuckDBExecutePreparedStreaming(statement, out var queryResult)
            : NativeMethods.PreparedStatements.DuckDBExecutePrepared(statement, out queryResult);

        if (!status.IsSuccess())
        {
            var errorMessage = NativeMethods.Query.DuckDBResultError(ref queryResult);
            var errorType = NativeMethods.Query.DuckDBResultErrorType(ref queryResult);
            queryResult.Close();

            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = "DuckDB execution failed";
            }

            statementTracer?.SetState(status, errorType, errorMessage);

            if (errorType == DuckDBErrorType.Interrupt)
            {
                throw new OperationCanceledException();
            }

            var innerException = UdfExceptionStore.Retrieve(connection);
            throw innerException != null
                ? new DuckDBException(errorMessage, innerException)
                : new DuckDBException(errorMessage, errorType);
        }

        statementTracer?.AcquireMetrics();
        StatementProfilerTracer.Current = null;

        return queryResult;
    }

    private static void BindParameters(DuckDBPreparedStatement preparedStatement, DuckDBParameterCollection parameterCollection)
    {
        var expectedParameters = NativeMethods.PreparedStatements.DuckDBParams(preparedStatement);
        if (parameterCollection.Count < expectedParameters)
        {
            throw new InvalidOperationException($"Invalid number of parameters. Expected {expectedParameters}, got {parameterCollection.Count}");
        }

        if (parameterCollection.OfType<DuckDBParameter>().Any(p => !string.IsNullOrEmpty(p.ParameterName)))
        {
            foreach (DuckDBParameter param in parameterCollection)
            {
                var state = NativeMethods.PreparedStatements.DuckDBBindParameterIndex(preparedStatement, out var index, param.ParameterName);
                if (state.IsSuccess())
                {
                    BindParameter(preparedStatement, index, param);
                }
            }
        }
        else
        {
            for (var i = 0; i < expectedParameters; ++i)
            {
                var param = parameterCollection[i];
                BindParameter(preparedStatement, i + 1, param);
            }
        }
    }

    private static void BindParameter(DuckDBPreparedStatement preparedStatement, long index, DuckDBParameter parameter)
    {
        using var parameterLogicalType = NativeMethods.PreparedStatements.DuckDBParamLogicalType(preparedStatement, index);
        var duckDBType = NativeMethods.LogicalType.DuckDBGetTypeId(parameterLogicalType);

        using var duckDBValue = parameter.Value.ToDuckDBValue(parameterLogicalType, duckDBType, parameter.DbType);

        var result = NativeMethods.PreparedStatements.DuckDBBindValue(preparedStatement, index, duckDBValue);

        if (!result.IsSuccess())
        {
            var errorMessage = NativeMethods.PreparedStatements.DuckDBPrepareError(preparedStatement);
            throw new InvalidOperationException($"Unable to bind parameter {index}: {errorMessage}");
        }
    }

    public void Dispose()
    {
        statement.Dispose();
    }
}