using DuckDB.NET.Data.Profiling;
using System.Collections.Frozen;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DuckDB.NET.Data.Extensions
{
    internal static class DuckDBMetricsExtensions
    {
        // Cache the snake_case conversions of DuckDBProfilingFormat enum values for efficient lookup.
        private static readonly FrozenDictionary<DuckDBProfilingFormat, string> ProfilingFormatCache =
            Enum.GetValues<DuckDBProfilingFormat>()
                .ToFrozenDictionary(f => f, f => JsonNamingPolicy.SnakeCaseLower.ConvertName(f.ToString()));

        // Cache the snake_case conversions of DuckDBMetricType enum values for efficient lookup.
        private static readonly FrozenDictionary<DuckDBMetricType, string> MetricTypeCache =
            Enum.GetValues<DuckDBMetricType>()
                .ToFrozenDictionary(m => m, m => JsonNamingPolicy.SnakeCaseUpper.ConvertName(m.ToString()));

        // Create a reverse lookup cache for string to DuckDBMetricType conversions.
        private static readonly FrozenDictionary<string, DuckDBMetricType> StringToMetricTypeCache =
            MetricTypeCache.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

        // create a reverse lookup cache for string to DuckDBProfilingFormat conversions.
        private static readonly FrozenDictionary<string, DuckDBProfilingFormat> StringToProfilingFormatCache =
            ProfilingFormatCache.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToDuckDBMetricTypeCollectionString(this DuckDBMetricTypeCollection metrics)
        {
            return $"{{{string.Join(",", metrics.Select(m => $"\"{m.ToDuckDBMetricTypeString()}\": \"TRUE\""))}}}";
        }

        /// <summary>
        /// Converts the specified DuckDB profiling format to its corresponding string representation.
        /// </summary>
        /// <param name="format">The DuckDB profiling format to convert.</param>
        /// <returns>A string that represents the specified DuckDB profiling format.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToDuckDBProfilingFormatString(this DuckDBProfilingFormat format)
        {
            return ProfilingFormatCache[format];
        }

        /// <summary>
        /// Converts the specified DuckDB metric type to its corresponding string representation.
        /// </summary>
        /// <param name="metricType">The DuckDB metric type to convert.</param>
        /// <returns>A string that represents the specified DuckDB metric type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToDuckDBMetricTypeString(this DuckDBMetricType metricType)
        {
            if (metricType == DuckDBMetricType.RowsReturned)
            {
                throw new DuckDBException($"The '{DuckDBMetricType.RowsReturned}' metric is not supported yet.", DuckDBErrorType.InvalidInput);
            }

            return MetricTypeCache[metricType];
        }

        /// <summary>
        /// Attempts to parse the specified string into a corresponding DuckDBMetricType value.
        /// </summary>
        /// <param name="metricTypeString">The string representation of the DuckDB metric type to parse. Cannot be null.</param>
        /// <param name="metricType">When this method returns, contains the DuckDBMetricType value equivalent to the parsed string, if the conversion
        /// succeeded; otherwise, the default value.</param>
        /// <returns>true if the string was successfully parsed into a DuckDBMetricType value; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryParseDuckDBMetricType(string metricTypeString, out DuckDBMetricType metricType)
        {
            return StringToMetricTypeCache.TryGetValue(metricTypeString, out metricType);
        }

        /// <summary>
        /// Attempts to parse the specified string into a corresponding DuckDB profiling format value.
        /// </summary>
        /// <remarks>Use this method to safely convert a string to a DuckDB profiling format without
        /// throwing an exception if the conversion fails.</remarks>
        /// <param name="profilingFormatString">The string representation of the DuckDB profiling format to parse. This value is compared case-sensitively
        /// against known profiling format names.</param>
        /// <param name="profilingFormat">When this method returns, contains the parsed DuckDB profiling format value if the parse operation succeeds;
        /// otherwise, contains the default value.</param>
        /// <returns>true if the string was successfully parsed into a DuckDB profiling format; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryParseDuckDBProfilingFormat(string profilingFormatString, out DuckDBProfilingFormat profilingFormat)
        {
            return StringToProfilingFormatCache.TryGetValue(profilingFormatString, out profilingFormat);
        }

    }
}
