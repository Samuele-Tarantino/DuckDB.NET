using DuckDB.NET.Data.Profiling.Statistics;

internal sealed class ProfilingInfo: IDisposable
{
    private readonly DuckDBNativeConnection connection;
    private DuckDBProfilingInfoWrapper? duckDBProfilingInfoWrapper;
    private bool isDisposed = false;

    internal ProfilingInfo(DuckDBNativeConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>
    /// Tries to prepare the profiling info by retrieving it from the connection. Returns true if successful, false otherwise.
    /// </summary>
    /// <returns></returns>
    internal bool TryPrepare()
    {
        var profilingInfo = DuckDBProfilingInfoWrapper.GetProfilingInfo(connection);

        if (profilingInfo == null)
        {
            return false;
        }

        duckDBProfilingInfoWrapper = profilingInfo;
        return true;
    }

    /// <summary>
    /// Gets the raw metrics as a dictionary of string key-value pairs. Throws an exception if the profiling info is not prepared.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal Dictionary<string, string> GetRawMetrics()
    {
        if (duckDBProfilingInfoWrapper == null)
        {
            throw new InvalidOperationException("Profiling info is not prepared. Call TryPrepare() first.");
        }

        using var metricsValue = duckDBProfilingInfoWrapper.GetMetrics();
        if (metricsValue.IsNull())
        {
            return [];
        }
        return metricsValue.GetMapValue<string, string>();
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            duckDBProfilingInfoWrapper?.Dispose();
            isDisposed = true;
        }
    }
}