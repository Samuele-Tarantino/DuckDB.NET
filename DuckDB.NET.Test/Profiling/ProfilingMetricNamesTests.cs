using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Data.Profiling.Statistics;
using System.Reflection;

namespace DuckDB.NET.Test.Profiling;

public class ProfilingMetricNamesTests
{
    private static ProfilingOptions Options(IReadOnlyCollection<string>? tracked = null) => new()
    {
        Coverage = DuckDBProfilingCoverage.All,
        Format = DuckDBProfilingFormat.Json,
        Mode = DuckDBProfilingMode.Standard,
        TrackedMetrics = tracked
    };

    private static ProfilingInfoMetrics Profile(ProfilingOptions options, string sql)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        connection.EnableProfiling(options);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) { }
        }

        return connection.RetrieveStatistics().QuerySummaryList.Last().StatementSummaries.Last().Metrics;
    }

    [Fact]
    public void TrackedMetricsAreReportedByName()
    {
        var metrics = Profile(Options([DuckDBMetricNames.QuerySql, DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.SystemPeakBufferMemory]), "SELECT 42");

        metrics.Keys.Should().BeEquivalentTo(DuckDBMetricNames.QuerySql, DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.SystemPeakBufferMemory);
        metrics[DuckDBMetricNames.QuerySql].Should().Be("SELECT 42");
    }

    [Fact]
    public void TrackedMetricsAcceptGlobs()
    {
        var metrics = Profile(Options([DuckDBMetricNames.IoAll, DuckDBMetricNames.PlannerAll]), "SELECT 42");

        metrics.Keys.Should().Contain([DuckDBMetricNames.IoTotalBytesRead, DuckDBMetricNames.PlannerTotalTime, DuckDBMetricNames.PlannerBindingTime]);
        metrics.Keys.Should().OnlyContain(name => name.StartsWith("io.") || name.StartsWith("planner."));
    }

    [Fact]
    public void SharedTotalTimeKeyIsAttributedToTheTrackedGroup()
    {
        var planner = Profile(Options([DuckDBMetricNames.PlannerTotalTime]), "SELECT 42");
        planner.Keys.Should().BeEquivalentTo(DuckDBMetricNames.PlannerTotalTime);

        var both = Profile(Options([DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.PlannerTotalTime]), "SELECT 42");
        both.Keys.Should().BeEquivalentTo(DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.PlannerTotalTime);
    }

    [Fact]
    public void DefaultMetricsAreTrackedWhenNoneAreSelected()
    {
        var metrics = Profile(Options(), "SELECT 42");

        metrics.Keys.Should().Contain([DuckDBMetricNames.QuerySql, DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.QueryCpuTime, DuckDBMetricNames.IoTotalBytesRead]);
        metrics.ResultSetSizeBytes.Should().NotBeNull();
    }

    [Theory]
    [InlineData("SELECT 1", 4UL)]
    [InlineData("SELECT i FROM range(1000) t(i)", 8000UL)]
    [InlineData("SELECT i::INTEGER FROM range(1000) t(i)", 4000UL)]
    [InlineData("SELECT i::VARCHAR FROM range(1000) t(i)", 16000UL)]
    [InlineData("SELECT i, i * 2 AS j FROM range(100000) t(i)", 1600000UL)]
    [InlineData("SELECT i FROM range(100000) t(i) WHERE i % 10 = 0", 80000UL)]
    [InlineData("SELECT count(*) FROM range(100000)", 8UL)]
    public void ResultSetSizeIsTheSizeOfTheRootOperatorOutput(string sql, ulong expectedBytes)
    {
        // Same values DuckDB 1.5 reported as RESULT_SET_SIZE.
        var metrics = Profile(Options([DuckDBMetricNames.OperatorType, DuckDBMetricNames.OperatorIntermediateSizeBytes]), sql);

        metrics.ResultSetSizeBytes.Should().Be(expectedBytes);
    }

    [Fact]
    public void ResultSetSizeIsNotAvailableWithoutTheOperatorMetrics()
    {
        Profile(Options([DuckDBMetricNames.QuerySql]), "SELECT 42").ResultSetSizeBytes.Should().BeNull();
        Profile(Options([DuckDBMetricNames.OperatorIntermediateSizeBytes]), "SELECT 42").ResultSetSizeBytes.Should().BeNull();
    }

    [Fact]
    public void TotalIntermediateRowsMatchTheRowsProducedByTheOperators()
    {
        // Same values DuckDB 1.5 reported as CUMULATIVE_CARDINALITY.
        var metrics = Profile(Options([DuckDBMetricNames.QueryTotalIntermediateRows]), "SELECT i FROM range(100000) t(i) WHERE i % 10 = 0");

        metrics[DuckDBMetricNames.QueryTotalIntermediateRows].Should().Be("110000");
    }

    [Fact]
    public void EveryMetricNameExistsInDuckDB()
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT metric_name FROM duckdb_available_metrics()";

        var available = new HashSet<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read()) available.Add(reader.GetString(0));
        }

        var names = typeof(DuckDBMetricNames).GetFields(BindingFlags.Public | BindingFlags.Static)
                                             .Where(f => f.IsLiteral)
                                             .Select(f => (string)f.GetRawConstantValue()!);

        foreach (var name in names)
        {
            var exists = name.EndsWith('*') ? available.Any(a => a.StartsWith(name[..^1])) : available.Contains(name);
            exists.Should().BeTrue($"{name} must be a DuckDB metric");
        }
    }
}
