using System.Diagnostics;

namespace DuckDB.NET.Data.Common
{
    internal sealed class TimerUtils
    {
        private static readonly long Frequency = Stopwatch.Frequency;

        /// <summary>
        /// Gets the current timestamp value for high-resolution timing operations.
        /// </summary>
        /// <remarks>The returned value can be used with Stopwatch.Frequency to calculate elapsed time intervals with high
        /// precision. This method is intended for scenarios where precise timing is required, such as performance
        /// measurements.</remarks>
        /// <returns>A long integer representing the current timestamp, as provided by the system's high-resolution performance counter.</returns>
        internal static long TimerCurrent() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Converts a timer value to its equivalent duration in milliseconds.
        /// </summary>
        /// <remarks>The conversion uses the timer's frequency to determine the number of milliseconds represented by the
        /// timer value. Ensure that the timer value and frequency are based on the same timer source.</remarks>
        /// <param name="timerValue">The timer value to convert, typically representing elapsed timer ticks.</param>
        /// <returns>The equivalent duration in milliseconds calculated from the specified timer value.</returns>
        internal static long TimerToMilliseconds(long timerValue)
            => timerValue * 1000 / Frequency;

        /// <summary>
        /// Calculates the elapsed number of ticks between two tick count values.
        /// </summary>
        /// <param name="startTick">The starting tick count value.</param>
        /// <param name="endTick">The ending tick count value.</param>
        /// <returns>The difference between the ending and starting tick count values, representing the elapsed ticks.</returns>
        internal static long CalculateTickCountElapsed(long startTick, long endTick)
            => endTick - startTick; // keep as long, no truncation
    }
}
