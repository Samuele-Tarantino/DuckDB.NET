namespace DuckDB.NET.Data.Profiling.Statistics
{
    internal abstract class ExecutionStatisticsBase(DuckDBNativeConnection duckDBNativeConnection)
    {
        protected readonly DuckDBNativeConnection duckDBNativeConnection = duckDBNativeConnection;

        protected DuckDBState state;
        protected string? errorMessage;

        internal abstract void Reset();

        internal abstract void StartTimer();

        internal abstract void StopTimer();

        internal virtual void SetState(DuckDBState state)
        {
            this.state = state;
        }

        internal virtual void SetState(DuckDBState state, string message)
        {
            this.state = state;
            this.errorMessage = message;
        }

        internal virtual void SetState(DuckDBState state, DuckDBErrorType errorType, string message)
        {
            this.state = state;
            this.errorMessage = string.Format("%s error: %s", errorType, message);
        }

        internal virtual DuckDBState State => state;

        internal virtual string ErrorMessage => errorMessage ?? string.Empty;
    }
}