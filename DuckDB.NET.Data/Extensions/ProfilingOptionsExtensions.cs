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
        /// The DuckDB 2.0 metric names to track: the legacy <c>EnabledMetrics</c> mapped to their 2.0 names plus <c>TrackedMetrics</c>,
        /// or the default set when neither is given.
        /// </summary>
        public static IReadOnlyList<string> GetTrackedMetrics(this ProfilingOptions options)
        {
#pragma warning disable CS0618 // EnabledMetrics is deprecated but still honored.
            var names = MetricMapping.ToTrackedNames(options.EnabledMetrics, options.TrackedMetrics);
#pragma warning restore CS0618

            return names.Count > 0 ? names : MetricMapping.ToTrackedNames(DuckDBMetrics.DefaultMetrics, null);
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
