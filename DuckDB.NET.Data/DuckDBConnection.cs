using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Connection;
using DuckDB.NET.Data.Profiling;
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
    internal ConnectionStatistics? profilingInfo;
    private bool collectstats;
    private ProfilingOptions profilingOptions;

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

            profilingInfo?.Dispose();
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
        };

        // Enable profiling on the duplicated connection so it collects the same metrics/statistics
        if (ProfilingEnabled)
        {
            duplicatedConnection.EnableProfiling(true, profilingOptions);
        }

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

    public bool ProfilingEnabled => collectstats;

    /// <summary>
    /// Retrieves a summary of the collected profiling statistics, including connection time, execution time, and any relevant metrics. 
    /// If profiling is not enabled, this method will return an empty summary.
    /// </summary>
    /// <returns>A <see cref="ProfilingSummary"/> containing the collected statistics.</returns>
    public ProfilingSummary RetrieveStatistics()
    {
        if (profilingInfo != null)
        {
            UpdateStatistics();
            return profilingInfo.GetProfilingSummary();
        }
        else
        {
            return new ConnectionStatistics(NativeConnection, collectstats).GetProfilingSummary();
        }
    }

    /// <summary>
    /// Enables or disables profiling for the current connection, optionally applying the specified profiling options.
    /// </summary>
    /// <remarks>Profiling collects statistics about the connection's activity. If profiling is enabled while the
    /// connection is open, statistics collection begins immediately. Disabling profiling stops statistics collection and
    /// finalizes the current profile.</remarks>
    /// <param name="enabled">A value indicating whether profiling should be enabled. Set to <see langword="true"/> to enable profiling;
    /// otherwise, <see langword="false"/> to disable profiling.</param>
    /// <param name="options">The profiling options to apply when enabling profiling. If <see langword="null"/>, default profiling options are
    /// used. This parameter is ignored when disabling profiling.</param>
    public void EnableProfiling(bool enabled, ProfilingOptions? options = null)
    {
        if (collectstats == enabled)
        {
            return;
        }

        collectstats = enabled;

        if (enabled)
        {
            this.profilingOptions = options ?? new ProfilingOptions();  // use provided options or default options if null

            if (State == ConnectionState.Open)
            {
                InitProfiling();
            }
        }
        else
        {
            // stop
            profilingInfo?.closeTimestamp = TimerUtils.TimerCurrent();
            DisableProfiling();
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

        if (ProfilingEnabled)
        {
            profilingInfo = new ConnectionStatistics(NativeConnection, collectstats);
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
        if (!ProfilingEnabled)
        {
            throw new InvalidOperationException("Profiling must be enabled to edit profiling options.");
        }
        this.profilingOptions = options;

        profilingInfo?.Reset();
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
        if (ProfilingEnabled && State == ConnectionState.Open)
        {
            var state = NativeMethods.Query.DuckDBQuery(NativeConnection, "PRAGMA disable_profiling; PRAGMA disable_profile;", out _);
            if (!state.IsSuccess())
            {
                throw new DuckDBException("Error disabling profiling.");
            }
        }
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
        profilingInfo?.openTimestamp = TimerUtils.TimerCurrent();

        // Do not enable profiling again for duplicated in-memory connections; the original connection
        // already has profiling enabled and the duplicated connection shares the same underlying state.
        if (ProfilingEnabled && !inMemoryDuplication)
        {

            var state = NativeMethods.Query.DuckDBQuery(NativeConnection, $"SET enable_profiling = '{profilingOptions.Format.ToDuckDBProfilingFormatString()}'", out _);
            if (!state.IsSuccess())
            {
                throw new DuckDBException($"Error setting '{"enable_profiling"}' to '{profilingOptions.Format}'");
            }

            state = NativeMethods.Query.DuckDBQuery(NativeConnection, $"SET profiling_coverage = '{profilingOptions.Coverage}'", out _);
            if (!state.IsSuccess())
            {
                throw new DuckDBException($"Error setting '{"profiling_coverage"}' to '{profilingOptions.Coverage}'");
            }

            state = NativeMethods.Query.DuckDBQuery(NativeConnection, $"SET profiling_mode = '{profilingOptions.Mode}'", out _);
            if (!state.IsSuccess())
            {
                throw new DuckDBException($"Error setting '{"profiling_mode"}' to '{profilingOptions.Mode}'");
            }

            if (!string.IsNullOrEmpty(profilingOptions.OutputPath))
            {
                state = NativeMethods.Query.DuckDBQuery(NativeConnection, $"SET profiling_output = '{profilingOptions.OutputPath}'", out _);
                if (!state.IsSuccess())
                {
                    throw new DuckDBException($"Error setting '{"profiling_output"}' to '{profilingOptions.OutputPath}'");
                }
            }

            var enabledMetrics = profilingOptions.EnabledMetrics ?? new DuckDBMetricTypeCollection(DuckDBMetrics.DefaultMetrics);
            state = NativeMethods.Query.DuckDBQuery(NativeConnection, $"SET custom_profiling_settings = '{enabledMetrics.ToDuckDBMetricString()}'", out _);
            if (!state.IsSuccess())
            {
                DuckDBException? innerEx = null;

                if (enabledMetrics.Contains(DuckDBMetricType.RowsReturned))
                {
                    innerEx = new DuckDBException($"The '{DuckDBMetricType.RowsReturned}' metric is not supported yet.", DuckDBErrorType.InvalidInput);
                }
                    
                throw new DuckDBException($"Error setting '{"custom_profiling_settings"}' to '{enabledMetrics.ToDuckDBMetricString()}'", innerEx);
                
            }
        }
    }

    private void UpdateStatistics()
    {
        if (ConnectionState.Open == State)
        {
            // update timestamp
            profilingInfo?.closeTimestamp = TimerUtils.TimerCurrent();
        }
        // delegate the rest of the work to the SqlStatistics class
        profilingInfo?.UpdateStatistics();
    }
    #endregion
}
