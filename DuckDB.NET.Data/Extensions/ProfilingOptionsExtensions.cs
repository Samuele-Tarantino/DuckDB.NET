using DuckDB.NET.Data.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuckDB.NET.Data.Extensions
{
    internal static class ProfilingOptionsExtensions
    {
        public static string ToDuckDBProfilingOptionString(this ProfilingOptions options)
        {
            var sb = new StringBuilder();

            sb.AppendLine("CALL enable_profiling(");
            sb.AppendLine($"    format := '{options.Format.ToDuckDBProfilingFormatString()}',");
            sb.AppendLine($"    coverage := '{options.Coverage}',");
            sb.AppendLine($"    mode := '{options.Mode}'");

            if (!string.IsNullOrEmpty(options.OutputPath))
                sb.AppendLine($"    ,save_location := '{options.OutputPath.Replace("'", "''")}'");

            if (options.EnabledMetrics is { Count: > 0 })
                sb.AppendLine($"    ,metrics := '{(options.EnabledMetrics).ToDuckDBMetricTypeCollectionString()}'");
            else
                sb.AppendLine($"    ,metrics := '{DuckDBMetrics.DefaultMetrics.ToDuckDBMetricTypeCollectionString()}'");

            sb.AppendLine(");");

            // If no output path is provided, disable profiling output to file
            if (string.IsNullOrEmpty(options.OutputPath))
                sb.AppendLine("SET profiling_output = '';");

            return sb.ToString();
        }
    }
}
