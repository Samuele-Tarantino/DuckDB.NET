using DuckDB.NET.Data.Connection;
using DuckDB.NET.Data.Profiling.Statistics;
using System.Linq;

namespace DuckDB.NET.Data.PreparedStatement;

internal sealed class PreparedStatement : IDisposable
{
    private readonly DuckDBPreparedStatement statement;
    private StatementProfiler? statementProfiler;

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
            // Initialize the query profiler for the entire batch of statements. The profiler will be responsible for tracking the execution of all statements within this batch.
            var queryProfiler = stats?.CreateQueryProfiler(statementCount);
            queryProfiler?.StartTimer();

            if (statementCount <= 0)
            {
                var error = NativeMethods.ExtractStatements.DuckDBExtractStatementsError(extractedStatements);

                queryProfiler?.SetState(DuckDBState.Error, DuckDBErrorType.Parser, error);
                queryProfiler?.StopTimer();

                throw new DuckDBException(error);
            }

            for (int index = 0; index < statementCount; index++)
            {
                var status = NativeMethods.ExtractStatements.DuckDBPrepareExtractedStatement(connection, extractedStatements, index, out var statement);

                var statementProfiler = queryProfiler?.CreateStatementProfiler(index);

                if (status.IsSuccess())
                {
                    using var preparedStatement = new PreparedStatement(statement);
                    preparedStatement.statementProfiler = statementProfiler;

                    var result = preparedStatement.Execute(parameters, useStreamingMode, connection);

                    // Stop the query profiler after the last statement has been executed.
                    // This ensures that the total execution time for the entire batch of statements is accurately captured
                    // and not after the data retrieval of the last statement.
                    if (index == statementCount - 1)
                    {
                        queryProfiler?.StopTimer();
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

                    // Initialize the statement profiler for the current statement. This allows for detailed profiling of each individual statement within the batch

                    statementProfiler?.SetState(status, errorMessage);
                    queryProfiler?.StopTimer(index);

                    throw new DuckDBException(errorMessage, UdfExceptionStore.Retrieve(connection));
                }
            }
        }
    }

    private DuckDBResult Execute(DuckDBParameterCollection parameterCollection, bool useStreamingMode, DuckDBNativeConnection connection)
    {
        // Start the statement profiler timer to measure the execution time of this statement.
        // The profiler will also capture any relevant metrics or state changes during execution.
        var profiler = this.statementProfiler;
        profiler?.StartTimer();

        try
        {

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

                profiler?.SetState(status, errorType, errorMessage);

                if (errorType == DuckDBErrorType.Interrupt)
                {
                    throw new OperationCanceledException();
                }

                var innerException = UdfExceptionStore.Retrieve(connection);
                throw innerException != null
                    ? new DuckDBException(errorMessage, innerException)
                    : new DuckDBException(errorMessage, errorType);
            }

            profiler?.AcquireMetrics();

            return queryResult;
        }
        finally
        {
            profiler?.StopTimer();
        }
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