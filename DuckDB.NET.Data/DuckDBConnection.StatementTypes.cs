namespace DuckDB.NET.Data;

public partial class DuckDBConnection
{
    /// <summary>
    /// Parses a SQL batch and returns the type of each statement without executing the batch.
    /// </summary>
    /// <param name="query">The SQL batch to parse.</param>
    /// <returns>The statement types in their execution order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuckDBException">Thrown when DuckDB cannot parse or prepare a statement in the batch.</exception>
    public IReadOnlyList<DuckDBStatementType> GetStatementTypes(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var statementCount = NativeMethods.ExtractStatements.DuckDBExtractStatements(NativeConnection, query, out var extractedStatements);
        using (extractedStatements)
        {
            if (statementCount <= 0)
            {
                var error = NativeMethods.ExtractStatements.DuckDBExtractStatementsError(extractedStatements);
                throw new DuckDBException(error);
            }

            var statementTypes = new List<DuckDBStatementType>(statementCount);
            for (var index = 0; index < statementCount; index++)
            {
                var status = NativeMethods.ExtractStatements.DuckDBPrepareExtractedStatement(NativeConnection, extractedStatements, index, out var statement);
                using (statement)
                {
                    if (!status.IsSuccess())
                    {
                        var error = NativeMethods.PreparedStatements.DuckDBPrepareError(statement);
                        throw new DuckDBException(string.IsNullOrEmpty(error) ? "DuckDB query could not be prepared." : error);
                    }

                    statementTypes.Add(NativeMethods.PreparedStatements.DuckDBPreparedStatementType(statement));
                }
            }

            return statementTypes;
        }
    }
}