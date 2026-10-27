using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Data.Profiling.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuckDB.NET.Data.Extensions
{
    internal static class ProfilingOptionsExtensions
    {
        /// <summary>
        /// The DuckDB 2.0 metric names to track: <c>TrackedMetrics</c>, or <c>DuckDBMetrics.DefaultTrackedMetrics</c> when it is not set.
        /// </summary>
        public static IReadOnlyList<string> GetTrackedMetrics(this ProfilingOptions options)
        {
            var names = ProfilingTreeReader.ToTrackedNames(options.TrackedMetrics);

            return names.Count > 0 ? names : ProfilingTreeReader.ToTrackedNames(DuckDBMetrics.DefaultTrackedMetrics);
        }

        public static string ToDuckDBProfilingOptionString(this ProfilingOptions options)
        {
            var sb = new StringBuilder();

            sb.AppendLine("CALL enable_profiling(");
            sb.AppendLine($"    format := '{options.Format.ToDuckDBProfilingFormatString()}',");
            sb.AppendLine($"    coverage := '{options.Coverage}',");
            sb.AppendLine($"    mode := '{options.Mode}'");

            if (!string.IsNullOrEmpty(options.OutputPath))
                sb.AppendLine($"    ,save_location := '{options.OutputPath.Replace("'", "''")}'");

            sb.AppendLine($"    ,metrics := [{string.Join(", ", options.GetTrackedMetrics().Select(m => $"'{m.Replace("'", "''")}'"))}]");

            sb.AppendLine(");");

            // If no output path is provided, disable profiling output to file
            if (string.IsNullOrEmpty(options.OutputPath))
                sb.AppendLine("SET profiling_output = '';");

            return sb.ToString();
        }
    }
}
