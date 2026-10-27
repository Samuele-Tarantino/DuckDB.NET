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
    /// Gets the metrics of the profiled query, flattened to qualified DuckDB 2.0 names. Throws an exception if the profiling info is not prepared.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal ProfilingTreeMetrics GetMetrics(IReadOnlyList<string>? tracked)
    {
        if (duckDBProfilingInfoWrapper == null)
        {
            throw new InvalidOperationException("Profiling info is not prepared. Call TryPrepare() first.");
        }

        return ProfilingTreeReader.Flatten(duckDBProfilingInfoWrapper, tracked);
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