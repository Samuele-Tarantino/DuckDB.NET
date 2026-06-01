namespace DuckDB.NET.Native;

public class DuckDBProfilingInfoWrapper : IDisposable
{
    private DuckDBProfilingInfo handle;
    private bool disposed;

    public DuckDBProfilingInfoWrapper(DuckDBProfilingInfo handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        this.handle = handle;
    }

    public static DuckDBProfilingInfoWrapper? GetProfilingInfo(DuckDBNativeConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var handle = NativeMethods.ProfilingInfo.DuckDBGetProfilingInfo(connection);
        if (handle.IsInvalid || handle.IsClosed)
        {
            Console.WriteLine("No profiling info");
            return null;
        }
        return new DuckDBProfilingInfoWrapper(handle);
    }

    public DuckDBValue GetValue(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return NativeMethods.ProfilingInfo.DuckDBProfilingInfoGetValue(handle, key);
    }

    public DuckDBValue GetMetrics()
    {
        return NativeMethods.ProfilingInfo.DuckDBProfilingInfoGetMetrics(handle);
    }

    public ulong GetChildCount()
    {
        return NativeMethods.ProfilingInfo.DuckDBProfilingInfoGetChildCount(handle);
    }

    public DuckDBProfilingInfoWrapper GetChild(ulong index)
    {
        var childHandle = NativeMethods.ProfilingInfo.DuckDBProfilingInfoGetChild(handle, index);
        if (childHandle.IsInvalid || childHandle.IsClosed)
        {
            throw new InvalidOperationException($"Failed to get child profiling info at index {index}.");
        }
        return new DuckDBProfilingInfoWrapper(childHandle);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            handle?.Dispose();
            disposed = true;
        }
    }
}