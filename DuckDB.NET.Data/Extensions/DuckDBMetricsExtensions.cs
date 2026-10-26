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

        // create a reverse lookup cache for string to DuckDBProfilingFormat conversions.
        private static readonly FrozenDictionary<string, DuckDBProfilingFormat> StringToProfilingFormatCache =
            ProfilingFormatCache.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

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
