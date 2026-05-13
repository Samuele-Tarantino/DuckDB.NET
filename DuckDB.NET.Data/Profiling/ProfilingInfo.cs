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

    internal ProfilingInfoMetrics GetMetrics()
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

        //var dic = metricsValue.GetMapValue<string, object>();
        //Console.WriteLine($"{new string(' ', 1 * 2 + 2)}Metrics:");
        //foreach (var kvp in dic)
        //{
        //    Console.WriteLine($"{new string(' ', 1 * 3 + 2)} {kvp.Key}:{kvp.Value}");
        //}

        return ProfilingInfoMetrics.FromMetricsDictionary(metricsValue.GetMapValue<string, object>());
    }

    private static void PrintProfilingNode(DuckDBProfilingInfoWrapper node, int indent)
    {
        // Example: print the 'name' and 'extra_info' keys if present
        var queryNameValue = node.GetValue("QUERY_NAME");
        Console.WriteLine($"{new string(' ', indent * 2)}QueryName: {queryNameValue.GetValue<string>()}");
        var nameValue = node.GetValue("CPU_TIME");
        ////var extraInfoValue = node.GetValue("extra_info");

        Console.WriteLine($"{new string(' ', indent * 2)}CpuTime: {nameValue.GetValue<string>()}");
        //if (!extraInfoValue.IsNull())
        //{
        //    Console.WriteLine($"{new string(' ', indent * 2 + 2)}Extra: {extraInfoValue.GetValue<string>()}");
        //}

        // Print metrics if needed
        var metrics = node.GetMetrics();
        if (!metrics.IsNull())
        {
            var dic = metrics.GetMapValue<string, object>();
            Console.WriteLine($"{new string(' ', indent * 2 + 2)}Metrics:");
            foreach (var kvp in dic)
            {
                Console.WriteLine($"{new string(' ', indent * 3 + 2)} {kvp.Key}:{kvp.Value}");
            }
        }

        //// Recurse into children
        //var childCount = node.GetChildCount();
        //for (ulong i = 0; i < childCount; i++)
        //{
        //    using var child = node.GetChild(i);
        //    PrintProfilingNode(child, indent + 1);
        //}
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