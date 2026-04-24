internal sealed class ProfilingInfo
{
    private readonly DuckDBNativeConnection _connection;
    private DuckDBProfilingInfoWrapper? duckDBProfilingInfoWrapper;

    internal ProfilingInfo(DuckDBNativeConnection connection)
    {
        _connection = connection;
    }

    internal bool TryPrepare()
    {
        // 1. Get the profiling info root node
        using var profilingInfo = DuckDBProfilingInfoWrapper.GetProfilingInfo(_connection);

        if (profilingInfo == null)
        {
            return false;
        }

        duckDBProfilingInfoWrapper = profilingInfo;

        // 2. Print the profiling info recursively
        //PrintProfilingNode(profilingInfo, 0);
        return true;
    }

    internal IDictionary<string, object> GetMetrics()
    {
        if (duckDBProfilingInfoWrapper == null)
        {
            throw new InvalidOperationException("Profiling info is not prepared. Call Prepare() first.");
        }

        var metricsValue = duckDBProfilingInfoWrapper.GetMetrics();
        if (metricsValue.IsNull())
        {
            return new Dictionary<string, object>();
        }
        return metricsValue.GetMapValue<string, object>();
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
}