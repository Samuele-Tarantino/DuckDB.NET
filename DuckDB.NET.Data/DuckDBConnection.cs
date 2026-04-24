using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Connection;
using DuckDB.NET.Data.Profiling.Statistics;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DuckDB.NET.Data;

public partial class DuckDBConnection : DbConnection
{
    private readonly ConnectionManager connectionManager = ConnectionManager.Default;
    private ConnectionState connectionState = ConnectionState.Closed;
    private DuckDBConnectionString? parsedConnection;
    private ConnectionReference? connectionReference;
    private bool inMemoryDuplication = false;
    private static readonly StateChangeEventArgs FromClosedToOpenEventArgs = new(ConnectionState.Closed, ConnectionState.Open);
    private static readonly StateChangeEventArgs FromOpenToClosedEventArgs = new(ConnectionState.Open, ConnectionState.Closed);

    // Statistics support
    internal SqlStatistics statistics;
    private bool collectstats;

    #region Protected Properties

    protected override DbProviderFactory? DbProviderFactory => DuckDBClientFactory.Instance;

    #endregion

    internal DuckDBTransaction? Transaction { get; set; }

    internal DuckDBConnectionString ParsedConnection => parsedConnection ??= DuckDBConnectionStringBuilder.Parse(ConnectionString);

    internal SqlStatistics Statistics
    {
        get => statistics;
    }

    public DuckDBConnection()
    {
        ConnectionString = string.Empty;
    }

    public DuckDBConnection(string connectionString)
    {
        ConnectionString = connectionString;
    }

    [AllowNull]
    [DefaultValue("")]
    public override string ConnectionString { get; set; }

    public override string Database
    {
        get
        {
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                return ParsedConnection.DataSource;
            }

            throw new InvalidOperationException("Connection string must be specified.");
        }
    }

    public override string DataSource
    {
        get
        {
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                return ParsedConnection!.DataSource;
            }

            throw new InvalidOperationException("Connection string must be specified.");
        }
    }

    /// <summary>
    /// Returns the native connection object that can be used to call DuckDB C API functions.
    /// </summary>
    public DuckDBNativeConnection NativeConnection => connectionReference?.NativeConnection
                                                      ?? throw new InvalidOperationException("The DuckDBConnection must be open to access the native connection.");

    public DuckDBDatabase NativeDatabase => connectionReference?.FileReferenceCounter.Database
                                              ?? throw new InvalidOperationException("The DuckDBConnection must be open to access the native database.");


    /// <summary>
    /// Enables or disables profiling of the connection. When enabled, the connection will collect statistics about query execution times and other relevant metrics.
    /// </summary>
    [DefaultValue(false)]
    public bool ProfilingEnabled
    {
        get
        {
            return (collectstats);
        }
        set
        {
            if (State != ConnectionState.Open)
            {
                throw new InvalidOperationException("The DuckDBConnection must be open to start collecting statistics.");
            }

            if (value)
            {
                // start
                if (statistics == null)
                {
                    statistics = new SqlStatistics(NativeConnection, value);
                }
            }
            else
            {
                // stop
                if (statistics != null)
                {
                    statistics.closeTimestamp = TimerUtils.TimerCurrent();
                }
            }
            collectstats = value;
        }
    }

    /// <summary>
    /// Retrieves the current set of SQL statistics as a dictionary.
    /// </summary>
    /// <returns>A dictionary containing the current SQL statistics. If no statistics are available, returns an empty dictionary.</returns>
    public IDictionary RetrieveStatistics()
    {
        if (Statistics != null)
        {
            UpdateStatistics();
            return Statistics.GetDictionary();
        }
        else
        {
            return new SqlStatistics(NativeConnection, collectstats).GetDictionary();
        }
    }

    /// <summary>
    /// Sets the minimum execution time, in milliseconds, required for a query plan to be collected for analysis.
    /// </summary>
    /// <param name="threshold">The minimum duration, in milliseconds, that a query must run before its plan is collected. Must be a
    /// non-negative integer.</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not open.</exception>
    public void QueryPlanCollectionThreshold(int threshold)
    {
        throw new NotImplementedException();
    }

    private void UpdateStatistics()
    {
        if (ConnectionState.Open == State)
        {
            // update timestamp
            statistics.closeTimestamp = TimerUtils.TimerCurrent();
        }
        // delegate the rest of the work to the SqlStatistics class
        Statistics.UpdateStatistics();
    }

    public override string ServerVersion => NativeMethods.Startup.DuckDBLibraryVersion();

    public override ConnectionState State => connectionState;

    public override void ChangeDatabase(string databaseName)
    {
        throw new NotSupportedException();
    }

    public override void Close()
    {
        if (connectionState == ConnectionState.Closed)
        {
            throw new InvalidOperationException("Connection is already closed.");
        }

        if (connectionReference is not null) //Should always be the case
        {
            connectionManager.ReturnConnectionReference(connectionReference);
        }

        UpdateStatistics();

        connectionState = ConnectionState.Closed;

        OnStateChange(FromOpenToClosedEventArgs);
    }

    public override void Open()
    {
        if (connectionState == ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection is already open.");
        }

        //In case of inMemoryDuplication, we can safely take the hypothesis that connectionReference is already assigned
        connectionReference = inMemoryDuplication ? connectionManager.DuplicateConnectionReference(connectionReference!)
                                                  : connectionManager.GetConnectionReference(ParsedConnection);

        connectionState = ConnectionState.Open;

        statistics.openTimestamp = TimerUtils.TimerCurrent();

        OnStateChange(FromClosedToOpenEventArgs);
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        return BeginTransaction(isolationLevel);
    }

    public new DuckDBTransaction BeginTransaction()
    {
        return BeginTransaction(IsolationLevel.Unspecified);
    }

    private new DuckDBTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        EnsureConnectionOpen();
        if (Transaction != null)
        {
            throw new InvalidOperationException("Already in a transaction.");
        }

        return Transaction = new DuckDBTransaction(this, isolationLevel);
    }

    protected override DbCommand CreateDbCommand()
    {
        return CreateCommand();
    }

    public new virtual DuckDBCommand CreateCommand()
    {
        return new DuckDBCommand
        {
            Connection = this,
            Transaction = Transaction
        };
    }

    public DuckDBAppender CreateAppender(string table) => CreateAppender(null, null, table);

    public DuckDBAppender CreateAppender(string? schema, string table) => CreateAppender(null, schema, table);

    public DuckDBAppender CreateAppender(string? catalog, string? schema, string table)
    {
        EnsureConnectionOpen();

        var appenderState = NativeMethods.Appender.DuckDBAppenderCreateExt(NativeConnection, catalog, schema, table, out var nativeAppender);

        if (!appenderState.IsSuccess())
        {
            try
            {
                DuckDBAppender.ThrowLastError(nativeAppender);
            }
            finally
            {
                nativeAppender.Close();
            }
        }

        return new DuckDBAppender(nativeAppender, GetTableName());

        string GetTableName()
        {
            return string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        }
    }

    /// <summary>
    /// Creates a type-safe appender using an AppenderMap for property-to-column mappings.
    /// </summary>
    /// <typeparam name="T">The type to append</typeparam>
    /// <typeparam name="TMap">The AppenderMap type defining the mappings</typeparam>
    /// <param name="table">The table name</param>
    /// <returns>A type-safe mapped appender</returns>
    public DuckDBMappedAppender<T, TMap> CreateAppender<T, TMap>(string table)
        where TMap : Mapping.DuckDBAppenderMap<T>, new()
    {
        return CreateAppender<T, TMap>(null, null, table);
    }

    /// <summary>
    /// Creates a type-safe appender using an AppenderMap for property-to-column mappings.
    /// </summary>
    /// <typeparam name="T">The type to append</typeparam>
    /// <typeparam name="TMap">The AppenderMap type defining the mappings</typeparam>
    /// <param name="schema">The schema name</param>
    /// <param name="table">The table name</param>
    /// <returns>A type-safe mapped appender</returns>
    public DuckDBMappedAppender<T, TMap> CreateAppender<T, TMap>(string? schema, string table)
        where TMap : Mapping.DuckDBAppenderMap<T>, new()
    {
        return CreateAppender<T, TMap>(null, schema, table);
    }

    /// <summary>
    /// Creates a type-safe appender using an AppenderMap for property-to-column mappings.
    /// </summary>
    /// <typeparam name="T">The type to append</typeparam>
    /// <typeparam name="TMap">The AppenderMap type defining the mappings</typeparam>
    /// <param name="catalog">The catalog name</param>
    /// <param name="schema">The schema name</param>
    /// <param name="table">The table name</param>
    /// <returns>A type-safe mapped appender</returns>
    public DuckDBMappedAppender<T, TMap> CreateAppender<T, TMap>(string? catalog, string? schema, string table)
        where TMap : Mapping.DuckDBAppenderMap<T>, new()
    {
        var appender = CreateAppender(catalog, schema, table);
        return new DuckDBMappedAppender<T, TMap>(appender);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // this check is to ensure exact same behavior as previous version
            // where Close() was calling Dispose(true) instead of the other way around.
            if (connectionState == ConnectionState.Open)
            {
                Close();
            }
        }

        base.Dispose(disposing);
    }

    private void EnsureConnectionOpen([CallerMemberName] string operation = "")
    {
        if (State != ConnectionState.Open)
        {
            throw new InvalidOperationException($"{operation} requires an open connection");
        }
    }

    public DuckDBConnection Duplicate()
    {
        EnsureConnectionOpen();

        // We're sure that the connectionString is not null because we previously checked the connection was open
        if (!ParsedConnection!.InMemory)
        {
            throw new NotSupportedException("Duplication of the connection is only supported for in-memory connections.");
        }

        var duplicatedConnection = new DuckDBConnection(ConnectionString)
        {
            parsedConnection = ParsedConnection,
            inMemoryDuplication = true,
            connectionReference = connectionReference,
            ProfilingEnabled = ProfilingEnabled
        };

        return duplicatedConnection;
    }

    public override DataTable GetSchema() =>
        GetSchema(DbMetaDataCollectionNames.MetaDataCollections);

    public override DataTable GetSchema(string collectionName) =>
        GetSchema(collectionName, null);

    public override DataTable GetSchema(string collectionName, string?[]? restrictionValues) =>
        DuckDBSchema.GetSchema(this, collectionName, restrictionValues);

    public DuckDBQueryProgress GetQueryProgress()
    {
        EnsureConnectionOpen();
        return NativeMethods.Startup.DuckDBQueryProgress(NativeConnection);
    }
}
