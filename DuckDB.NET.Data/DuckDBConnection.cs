using DuckDB.NET.Data.Connection;
using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Data.Profiling.Statistics;
using DuckDB.NET.Data.Profiling.Statistics.Summary;
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
    private ConnectionStatistics? statistics;
    private bool profilingEnabled;
    private ProfilingOptions? profilingOptions;

    #region Protected Properties

    protected override DbProviderFactory? DbProviderFactory => DuckDBClientFactory.Instance;

    #endregion

    internal DuckDBTransaction? Transaction { get; set; }

    internal DuckDBConnectionString ParsedConnection => parsedConnection ??= DuckDBConnectionStringBuilder.Parse(ConnectionString);

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

    /// <summary>
    /// Gets the underlying native DuckDB database instance associated with the current connection.
    /// </summary>
    public DuckDBDatabase NativeDatabase => connectionReference?.FileReferenceCounter.Database
                                              ?? throw new InvalidOperationException("The DuckDBConnection must be open to access the native database.");

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

        // If profilingOptions has no explicit value for this connection, inherit the file-backed DB settings.
        // Otherwise persist the explicit options to the shared FileReference so other connections see them.
        if (!profilingOptions.HasValue)
        {
            profilingEnabled = connectionReference.FileReferenceCounter.IsProfilingEnabled;
            profilingOptions = connectionReference.FileReferenceCounter.ProfilingOptions;
        }
        else
        {
            connectionReference.FileReferenceCounter.IsProfilingEnabled = profilingEnabled;
            connectionReference.FileReferenceCounter.ProfilingOptions = profilingOptions.Value;
        }

        InitProfiling();

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

            statistics?.Dispose();
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
            profilingEnabled = profilingEnabled,
            profilingOptions = profilingOptions,
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

    #region profiling

    public bool IsProfilingEnabled => profilingEnabled;

    /// <summary>
    /// Retrieves a summary of the collected profiling statistics, including connection time, execution time, and any relevant metrics.
    /// If profiling is not enabled, this method will return an empty summary.
    /// </summary>
    /// <returns>A <see cref="ProfilingSummary"/> containing the collected statistics.</returns>
    public ProfilingSummary RetrieveStatistics()
    {
        if (statistics != null)
        {
            UpdateStatistics();
            return statistics.GetProfilingSummary();
        }
        else
        {
            // Profiling not enabled for this connection
            return new ProfilingSummary();
        }
    }

    /// <summary>
    /// Enables profiling for the current connection, optionally using the specified profiling options.
    /// </summary>
    /// <remarks>If profiling is enabled while the connection is already open, profiling is initialized immediately.
    /// Otherwise, profiling will be initialized when the connection is opened.</remarks>
    /// <param name="options">An optional set of profiling options to configure profiling behavior. If null, default options are used.</param>
    public void EnableProfiling(ProfilingOptions? options = null)
    {
        profilingEnabled = true;
        this.profilingOptions = options ?? new ProfilingOptions();  // use provided options or default options if null

        if (connectionReference?.FileReferenceCounter is { } fileRefCounter)
        {
            fileRefCounter.IsProfilingEnabled = true;
            fileRefCounter.ProfilingOptions = this.profilingOptions.Value;
        }

        if (State == ConnectionState.Open)
        {
            InitProfiling();
        }
    }

    /// <summary>
    /// Disables profiling for the current session, with an option to reset collected statistics.
    /// </summary>
    /// <remarks>If profiling is already disabled, calling this method has no effect. Resetting statistics
    /// clears all previously collected profiling data, which cannot be recovered.</remarks>
    /// <param name="resetStatistics">true to reset all collected profiling statistics after disabling profiling; otherwise, false.</param>
    public void DisableProfiling(bool resetStatistics = false)
    {
        // stop
        statistics?.StopTimer();

        if (resetStatistics)
        {
            ResetStatistics();
        }

        DisableProfiling();
    }

    /// <summary>
    /// Resets all collected profiling statistics to their initial state.
    /// </summary>
    /// <remarks>This method has no effect if profiling is not enabled. Use this method to clear accumulated
    /// profiling data before starting a new measurement period.</remarks>
    public void ResetStatistics()
    {
        if (IsProfilingEnabled)
        {
            statistics?.Reset();
        }
    }

    /// <summary>
    /// Initializes the profiling statistics for the current connection.
    /// </summary>
    /// <remarks>This method prepares internal data structures to collect and store profiling information
    /// related to the connection's activity. It should be called before attempting to access profiling or statistics
    /// data for the connection.</remarks>
    private void InitProfiling()
    {
        EnsureConnectionOpen();

        if (IsProfilingEnabled)
        {
            // Dispose any existing statistics instance to ensure fresh state and remove previous mapping
            // from the global ConnectionStatistics cache.
            try
            {
                statistics?.Dispose();
            }
            finally
            {
                statistics = new ConnectionStatistics(NativeConnection, profilingEnabled, this.profilingOptions);
            }
            LoadStatisticsProfile();
        }
    }

    /// <summary>
    /// Modifies the current profiling options used for collecting performance statistics.
    /// </summary>
    /// <param name="options">The new profiling options to apply. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if profiling is not enabled when attempting to edit profiling options.</exception>
    public void EditProfilingOptions(ProfilingOptions options)
    {
        if (!IsProfilingEnabled)
        {
            throw new InvalidOperationException("Profiling must be enabled to edit profiling options.");
        }

        this.profilingOptions = options;

        if (connectionReference?.FileReferenceCounter is { } fileRefCounter)
        {
            fileRefCounter.ProfilingOptions = options;
        }

        statistics?.Reset();
        LoadStatisticsProfile();
    }

    /// <summary>
    /// Disables query profiling for the current database connection if profiling is enabled.
    /// </summary>
    /// <remarks>This method has no effect if profiling is already disabled. Disabling profiling may improve
    /// performance for subsequent queries.</remarks>
    /// <exception cref="DuckDBException">Thrown if an error occurs while disabling profiling on the database connection.</exception>
    private void DisableProfiling()
    {
        if (IsProfilingEnabled && State == ConnectionState.Open)
        {
            var state = NativeMethods.Query.DuckDBQuery(NativeConnection, "CALL disable_profiling();", out var queryResult);
            EnsureStateIsSuccess(state, queryResult, "Error disabling profiling.");

            if (this.profilingOptions is { OutputPath: not null })
            {
                // If output path is set, we need to disable profiling at the end of each session to ensure the file is properly flushed and closed.
                state = NativeMethods.Query.DuckDBQuery(NativeConnection, "SET profiling_output = ''", out var queryResult2);
                EnsureStateIsSuccess(state, queryResult2, "Error disabling profiling.");
            }
        }

        this.profilingOptions = null;

        if (connectionReference?.FileReferenceCounter is { } fileRefCounter)
        {
            fileRefCounter.IsProfilingEnabled = false;
            fileRefCounter.ProfilingOptions = null;
        }

        statistics?.DisableQueryExecutionTracing();
        profilingEnabled = false;
    }

    /// <summary>
    /// Configures and enables query profiling for the current database connection based on the specified profiling
    /// options.
    /// </summary>
    /// <remarks>Profiling is only enabled for original in-memory connections; duplicated in-memory
    /// connections share the same profiling state and are not reconfigured. This method applies settings such as
    /// profiling format, coverage, mode, output path, and enabled metrics according to the current profiling
    /// options.</remarks>
    /// <exception cref="DuckDBException">Thrown if an error occurs while setting profiling configuration options.</exception>
    private void LoadStatisticsProfile()
    {
        statistics?.StartTimer();

        if (IsProfilingEnabled && profilingOptions is { } options)
        {
            // Build SQL and emit lightweight diagnostics to help reproduce issues when tests run together
            var sql = ProfilingOptionsExtensions.ToDuckDBProfilingOptionString(options);

            var state = NativeMethods.Query.DuckDBQuery(NativeConnection, sql, out var queryResult);
            EnsureStateIsSuccess(state, queryResult, "Error configuring profiling settings.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureStateIsSuccess(DuckDBState state, DuckDBResult result, string fallbackMessage)
    {
        try
        {
            if (!state.IsSuccess())
            {
                var errorMessage = NativeMethods.Query.DuckDBResultError(ref result);
                var errorType = NativeMethods.Query.DuckDBResultErrorType(ref result);

                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = fallbackMessage;
                }

                if (errorType == DuckDBErrorType.Interrupt)
                {
                    throw new OperationCanceledException();
                }

                var innerException = UdfExceptionStore.Retrieve(NativeConnection);
                throw innerException != null
                    ? new DuckDBException(errorMessage, innerException)
                    : new DuckDBException(errorMessage, errorType);
            }
        }
        finally
        {
            result.Close();
        }
    }

    private void UpdateStatistics()
    {
        if (ConnectionState.Open == State)
        {
            // update timestamp
            statistics?.StopTimer();
        }

        // delegate the rest of the work to the SqlStatistics class
        statistics?.UpdateStatistics();
    }
    #endregion
}
