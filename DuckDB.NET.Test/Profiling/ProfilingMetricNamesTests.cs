using DuckDB.NET.Data.Profiling;

namespace DuckDB.NET.Test.Profiling;

public class ProfilingMetricNamesTests
{
    private static ProfilingOptions Options(IReadOnlyCollection<string>? tracked = null, DuckDBMetricTypeCollection? legacy = null) => new()
    {
        Coverage = DuckDBProfilingCoverage.All,
        Format = DuckDBProfilingFormat.Json,
        Mode = DuckDBProfilingMode.Standard,
        TrackedMetrics = tracked,
        EnabledMetrics = legacy
    };

    private static Data.Profiling.Statistics.ProfilingInfoMetrics Profile(ProfilingOptions options, string sql)
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
        var metrics = Profile(Options(tracked: [DuckDBMetricNames.QuerySql, DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.SystemPeakBufferMemory]), "SELECT 42");

        metrics.ByName.Keys.Should().BeEquivalentTo(DuckDBMetricNames.QuerySql, DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.SystemPeakBufferMemory);
        metrics.ByName[DuckDBMetricNames.QuerySql].Should().Be("SELECT 42");
    }

    [Fact]
    public void TrackedMetricsAcceptGlobs()
    {
        var metrics = Profile(Options(tracked: [DuckDBMetricNames.IoAll, DuckDBMetricNames.PlannerAll]), "SELECT 42");

        metrics.ByName.Keys.Should().Contain([DuckDBMetricNames.IoTotalBytesRead, DuckDBMetricNames.PlannerTotalTime, DuckDBMetricNames.PlannerBindingTime]);
        metrics.ByName.Keys.Should().OnlyContain(name => name.StartsWith("io.") || name.StartsWith("planner."));
    }

    [Fact]
    public void SharedTotalTimeKeyIsAttributedToTheTrackedGroup()
    {
        var planner = Profile(Options(tracked: [DuckDBMetricNames.PlannerTotalTime]), "SELECT 42");
        planner.ByName.Keys.Should().BeEquivalentTo(DuckDBMetricNames.PlannerTotalTime);

        var query = Profile(Options(tracked: [DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.PlannerTotalTime]), "SELECT 42");
        query.ByName.Keys.Should().BeEquivalentTo(DuckDBMetricNames.QueryTotalTime, DuckDBMetricNames.PlannerTotalTime);
    }

    [Fact]
    public void LegacyMetricsAreMappedToNames()
    {
        var metrics = Profile(Options(legacy: [DuckDBMetricType.QueryName, DuckDBMetricType.Latency, DuckDBMetricType.CpuTime, DuckDBMetricType.TotalBytesRead]), "SELECT 42");

        metrics.Keys.Should().BeEquivalentTo([DuckDBMetricType.QueryName, DuckDBMetricType.Latency, DuckDBMetricType.CpuTime, DuckDBMetricType.TotalBytesRead]);
        metrics[DuckDBMetricType.QueryName].Should().Be(metrics.ByName[DuckDBMetricNames.QuerySql]);
        metrics[DuckDBMetricType.Latency].Should().Be(metrics.ByName[DuckDBMetricNames.QueryTotalTime]);
    }

    [Fact]
    public void LegacyAndTrackedMetricsAreCombined()
    {
        var metrics = Profile(Options(tracked: [DuckDBMetricNames.PlannerTotalTime], legacy: [DuckDBMetricType.QueryName]), "SELECT 42");

        metrics.ByName.Keys.Should().BeEquivalentTo(DuckDBMetricNames.QuerySql, DuckDBMetricNames.PlannerTotalTime);
        metrics.Keys.Should().BeEquivalentTo([DuckDBMetricType.QueryName, DuckDBMetricType.Planner]);
    }

    [Theory]
    [InlineData("SELECT 1", "4")]
    [InlineData("SELECT i FROM range(1000) t(i)", "8000")]
    [InlineData("SELECT i::INTEGER FROM range(1000) t(i)", "4000")]
    [InlineData("SELECT i::VARCHAR FROM range(1000) t(i)", "16000")]
    [InlineData("SELECT i, i * 2 AS j FROM range(100000) t(i)", "1600000")]
    [InlineData("SELECT i FROM range(100000) t(i) WHERE i % 10 = 0", "80000")]
    [InlineData("SELECT count(*) FROM range(100000)", "8")]
    public void ResultSetSizeIsTheSizeOfTheRootOperatorOutput(string sql, string expectedBytes)
    {
        // Same values DuckDB 1.5 reported as RESULT_SET_SIZE.
        var metrics = Profile(Options(legacy: [DuckDBMetricType.ResultSetSize]), sql);

        metrics[DuckDBMetricType.ResultSetSize].Should().Be(expectedBytes);
    }

    [Fact]
    public void CumulativeCardinalityIsTheTotalOfIntermediateRows()
    {
        var metrics = Profile(Options(legacy: [DuckDBMetricType.CumulativeCardinality]), "SELECT i FROM range(100000) t(i) WHERE i % 10 = 0");

        metrics[DuckDBMetricType.CumulativeCardinality].Should().Be("110000");
    }

    [Fact]
    public void EveryMappedLegacyMetricExistsInDuckDB()
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

        var notMapped = new[] { DuckDBMetricType.RowsReturned, DuckDBMetricType.OperatorName };

        foreach (var metric in Enum.GetValues<DuckDBMetricType>().Except(notMapped))
        {
            var name = Data.Profiling.Statistics.MetricMapping.ToName(metric);

            name.Should().NotBeNull($"{metric} must be mapped");
            (name!.EndsWith(".*") ? available.Any(a => a.StartsWith(name[..^1])) : available.Contains(name)).Should().BeTrue($"{metric} maps to {name}");
        }

        foreach (var metric in notMapped.Except([DuckDBMetricType.RowsReturned]))
        {
            Data.Profiling.Statistics.MetricMapping.ToName(metric).Should().BeNull();
        }
    }

    [Fact]
    public void RowsReturnedStaysUnsupported()
    {
        var act = () => Profile(Options(legacy: [DuckDBMetricType.RowsReturned]), "SELECT 1");

        act.Should().Throw<DuckDBException>();
    }
}
