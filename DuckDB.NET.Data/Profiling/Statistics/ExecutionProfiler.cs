using DuckDB.NET.Data.Common;

namespace DuckDB.NET.Data.Profiling.Statistics;

internal abstract class ExecutionProfiler(DuckDBNativeConnection duckDBNativeConnection, ProfilingOptions? profilingOptions) : IExecutionProfiler
{
    protected readonly DuckDBNativeConnection duckDBNativeConnection = duckDBNativeConnection;
    protected readonly ProfilingOptions? profilingOptions = profilingOptions;

    protected DuckDBState state;
    protected string? errorMessage;

    protected long? startTimestamp;
    protected long executionTime;
    protected DateTimeOffset startTime;
    protected DateTimeOffset endTime;

    internal long ExecutionTime => TimerUtils.TimerToMilliseconds(executionTime);
    internal DateTimeOffset StartTime => startTime;
    internal DateTimeOffset EndTime => endTime;

    /// <summary>
    /// Starts the timer, initiating the timing operation.
    /// </summary>
    public virtual void StartTimer()
    {
        if (!startTimestamp.HasValue)
        {
            startTimestamp = TimerUtils.TimerCurrent();
            startTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Stops the currently running timer, if active.
    /// </summary>
    /// <remarks>Call this method to halt the timer and record its elapsed time. If the timer is not running, calling
    /// this method has no effect.</remarks>
    public virtual void StopTimer()
    {
        ReleaseAndUpdateExecutionTimer();
    }

    /// <summary>
    /// Releases the current execution timer and updates the accumulated execution time and end time for the operation.
    /// </summary>
    protected virtual void ReleaseAndUpdateExecutionTimer()
    {
        if (startTimestamp.HasValue)
        {
            long elapsed = TimerUtils.CalculateTickCountElapsed(startTimestamp.Value, TimerUtils.TimerCurrent());
            executionTime += elapsed;

            // Convert the elapsed high-resolution ticks to a TimeSpan and apply to the wall-clock start time.
            var elapsedSpan = TimerUtils.TimerToTimeSpan(elapsed);
            endTime = startTime.Add(elapsedSpan);

            startTimestamp = null;
        }
    }

    /// <summary>
    /// Collects and updates performance or usage metrics for the current instance.
    /// </summary>
    public virtual void AcquireMetrics() { }

    /// <summary>
    /// Resets the object to its initial state.
    /// </summary>
    public virtual void Reset() { }

    /// <summary>
    /// Gets the current state of the current operation, indicating success or error
    /// </summary>
    public DuckDBState State => state;

    /// <summary>
    /// Gets the error message associated with the current operation.
    /// </summary>
    public string ErrorMessage => errorMessage ?? string.Empty;

    /// <summary>
    /// Sets the current state of the DuckDB connection or operation.
    /// </summary>
    /// <param name="state">The new state to assign to the DuckDB instance.</param>
    public void SetState(DuckDBState state) => this.state = state;

    /// <summary>
    /// Sets the current state and associated error message for the DuckDB instance.
    /// </summary>
    /// <param name="state">The new state to assign to the DuckDB instance.</param>
    /// <param name="message">The error message to associate with the specified state. Can be null or empty if no error message is required.</param>
    public void SetState(DuckDBState state, string message)
    {
        this.state = state;
        this.errorMessage = message;
    }

    /// <summary>
    /// Sets the current state and error information for the DuckDB connection or operation.
    /// </summary>
    /// <param name="state">The new state to assign to the DuckDB connection or operation.</param>
    /// <param name="errorType">The type of error associated with the state change. Used to categorize the error message.</param>
    /// <param name="message">A descriptive message providing details about the error or state change. Cannot be null.</param>
    public void SetState(DuckDBState state, DuckDBErrorType errorType, string message)
    {
        this.state = state;
        this.errorMessage = $"{errorType} error: {message}";
    }
}