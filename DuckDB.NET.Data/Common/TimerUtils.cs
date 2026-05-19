namespace DuckDB.NET.Data.Common
{
    internal sealed class TimerUtils
    {

        /// <summary>
        /// Gets the current UTC time as the number of ticks elapsed since 12:00:00 midnight, January 1, 0001.
        /// </summary>
        /// <returns>A 64-bit integer representing the current UTC time in ticks, where one tick equals 100 nanoseconds.</returns>
        internal static long TimerCurrent() => DateTimeOffset.UtcNow.UtcTicks;

        /// <summary>
        /// Gets the current date and time in Coordinated Universal Time (UTC).
        /// </summary>
        /// <returns>A <see cref="DateTimeOffset"/> value that represents the current UTC date and time.</returns>
        internal static DateTimeOffset Now() => DateTimeOffset.UtcNow;

        /// <summary>
        /// Calculates the elapsed number of ticks between two tick count values.
        /// </summary>
        /// <param name="startTick">The starting tick count value, typically representing the beginning of a time interval.</param>
        /// <param name="endTick">The ending tick count value, typically representing the end of a time interval.</param>
        /// <returns>The number of ticks that have elapsed between the start and end tick counts, as an unsigned 32-bit integer.</returns>
        internal static uint CalculateTickCountElapsed(long startTick, long endTick)
        {

            return (uint)(endTick - startTick);
        }

        /// <summary>
        /// Converts a timer value expressed in ticks to its equivalent value in milliseconds.
        /// </summary>
        /// <param name="timerValue">The timer value, in ticks, to convert to milliseconds.</param>
        /// <returns>The equivalent value in milliseconds as a 64-bit integer.</returns>
        internal static long TimerToMilliseconds(long timerValue)
        {
            long result = timerValue / TimeSpan.TicksPerMillisecond;
            return result;
        }
    }
}
