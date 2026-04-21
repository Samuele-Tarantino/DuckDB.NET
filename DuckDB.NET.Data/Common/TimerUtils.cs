namespace DuckDB.NET.Data.Common
{
    internal sealed class TimerUtils
    {

        internal static long TimerCurrent() => DateTimeOffset.UtcNow.UtcTicks;

        internal static DateTimeOffset Now() => DateTimeOffset.UtcNow;

        internal static uint CalculateTickCountElapsed(long startTick, long endTick)
        {

            return (uint)(endTick - startTick);
        }

        internal static long TimerToMilliseconds(long timerValue)
        {
            long result = timerValue / TimeSpan.TicksPerMillisecond;
            return result;
        }
    }
}
