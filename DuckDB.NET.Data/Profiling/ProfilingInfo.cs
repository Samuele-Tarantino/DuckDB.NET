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

    internal Dictionary<string, string> GetRawMetrics()
    {
        if (duckDBProfilingInfoWrapper == null)
        {
            throw new InvalidOperationException("Profiling info is not prepared. Call Prepare() first.");
        }
        var metricsValue = duckDBProfilingInfoWrapper.GetMetrics();
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