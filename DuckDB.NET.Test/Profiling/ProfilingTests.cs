using DuckDB.NET.Data.Profiling;

namespace DuckDB.NET.Test.Profiling;

public class ProfilingTests(DuckDBDatabaseFixture db) : DuckDBTestBase(db)
{

    [Fact]
    public void MetricsDictionaryContainsEnabledMetrics()
    {
        // enable profiling with specific metrics enabled
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName, DuckDBMetricType.CpuTime },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            var before = Connection.RetrieveStatistics();
            var beforeCount = before.QuerySummaryList.Length;

            // execute a small batch that contains different statement types
            Command.CommandText = "CREATE TABLE profiling_test(a INTEGER); INSERT INTO profiling_test VALUES (1); SELECT a FROM profiling_test;";
            using var reader = Command.ExecuteReader();

            // advance through all result sets to ensure statements are executed
            do { } while (reader.NextResult());

            var summary = Connection.RetrieveStatistics();
            // If no new summaries were produced, profiling might not be available in this environment.
            var newSummaries = summary.QuerySummaryList.Skip(beforeCount).ToArray();
            newSummaries.Length.Should().BeGreaterThan(0, "Expected new query summaries after executing batch");
            var last = newSummaries.Last();

            // check that enabled metrics appear in at least one statement's metrics
            var containsQueryName = last.Infos.Any(i => i != null && i.ContainsKey(DuckDBMetricType.QueryName));
            var containsCpuTime = last.Infos.Any(i => i != null && i.ContainsKey(DuckDBMetricType.CpuTime));

            containsQueryName.Should().BeTrue();
            containsCpuTime.Should().BeTrue();
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void MultipleEnabledMetricsArePresentInMetricsDictionary()
    {
        var metricsToEnable = new[]
        {
            DuckDBMetricType.QueryName,
            DuckDBMetricType.CpuTime,
            DuckDBMetricType.Latency,
            DuckDBMetricType.TotalBytesRead
        };

        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection(metricsToEnable),
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            var before = Connection.RetrieveStatistics();
            var beforeCount = before.QuerySummaryList.Length;

            Command.CommandText = "CREATE TABLE profiling_metrics(a INTEGER); INSERT INTO profiling_metrics VALUES (1); SELECT a FROM profiling_metrics;";
            using var reader = Command.ExecuteReader();
            do { } while (reader.NextResult());

            var summary = Connection.RetrieveStatistics();
            var newSummaries = summary.QuerySummaryList.Skip(beforeCount).ToArray();
            newSummaries.Length.Should().BeGreaterThan(0, "Expected new query summaries after executing batch");
            var last = newSummaries.Last();
            last.Infos.Should().NotBeNull();
            last.Infos.Length.Should().BeGreaterThan(0, "Expected some profiling info when metrics are enabled");

            // ensure at least one statement collected all requested metrics
            bool found = last.Infos.Any(info => info != null && metricsToEnable.All(m => info.ContainsKey(m)));

            found.Should().BeTrue("At least one statement should include all the enabled metrics (QueryName, CpuTime, Latency, TotalBytesRead)");
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SelectCoverageRespectsSelectPosition(int selectPosition)
    {
        var id = Guid.NewGuid().ToString("N");

        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.Select,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            var before = Connection.RetrieveStatistics();
            var beforeCount = before.QuerySummaryList.Length;

            var val = 200 + selectPosition;
            string a = $"t_{id}_a";
            string b = $"t_{id}_b";

            string cmd;
            if (selectPosition == 0)
            {
                cmd = $"SELECT {val}; CREATE TABLE {a}(i INTEGER); CREATE TABLE {b}(i INTEGER);";
            }
            else if (selectPosition == 1)
            {
                cmd = $"CREATE TABLE {a}(i INTEGER); SELECT {val}; CREATE TABLE {b}(i INTEGER);";
            }
            else
            {
                cmd = $"CREATE TABLE {a}(i INTEGER); CREATE TABLE {b}(i INTEGER); SELECT {val};";
            }

            Command.CommandText = cmd;
            using var reader = Command.ExecuteReader();
            do { } while (reader.NextResult());

            var summary = Connection.RetrieveStatistics();
            var newSummaries = summary.QuerySummaryList.Skip(beforeCount).ToArray();
            newSummaries.Length.Should().BeGreaterThan(0, "Expected new query summaries after executing batch");
            var last = newSummaries.Last();
            last.StatementCount.Should().Be(3);

            last.Infos.Should().NotBeNull();

            if (last.Infos.Length >= last.StatementCount)
            {
                for (int i = 0; i < last.StatementCount; i++)
                {
                    var info = i < last.Infos.Length ? last.Infos[i] : null;
                    if (i == selectPosition)
                    {
                        info.Should().NotBeNull();
                        info.Count.Should().BeGreaterThan(0);
                    }
                    else
                    {
                        info.Should().NotBeNull();
                        info.Count.Should().Be(0);
                    }
                }
            }
            else
            {
                // If Infos length doesn't map 1:1 to statements, at least ensure exactly one statement collected metrics
                var nonEmpty = last.Infos.Count(i => i != null && i.Count > 0);
                nonEmpty.Should().Be(1, "Expected exactly one statement to have metrics when coverage=Select");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void DisableStopsCollectingNewQueries()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            // run first batch
            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            var before = Connection.RetrieveStatistics();
            var beforeCount = before.QueryCount;

            // disable and run another batch
            Connection.DisableProfiling();
            Command.CommandText = "SELECT 2;";
            using (var r = Command.ExecuteReader()) { }

            var after = Connection.RetrieveStatistics();
            // after disabling, QueryCount should not have increased
            after.QueryCount.Should().Be(beforeCount);
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void ResetCleanCollectedStatistics()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            // run first batch
            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            // disable and run another batch
            Connection.ResetStatistics();
            var after = Connection.RetrieveStatistics();
            // after resetting, QueryCount should be zero
            after.QueryCount.Should().Be(0);
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void EnablingWithRowsReturnedMetricThrows()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.RowsReturned },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Action act = () => Connection.EnableProfiling(options);
        act.Should().Throw<DuckDBException>().Where(e => e.Message.Contains("RowsReturned") || (e.InnerException != null && e.InnerException.Message.Contains("RowsReturned")));
    }

    [Fact]
    public void CoverageSelectOnlyProfilesOnlySelectStatements()
    {
        // enable profiling for SELECT coverage only
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.Select,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            var before = Connection.RetrieveStatistics();
            var beforeCount = before.QuerySummaryList.Length;

            // execute a batch with CREATE, INSERT and SELECT (three statements)
            Command.CommandText = "CREATE TABLE profiling_cov(a INTEGER); INSERT INTO profiling_cov VALUES (1); SELECT a FROM profiling_cov;";
            using var reader = Command.ExecuteReader();

            // advance through all result sets
            do { } while (reader.NextResult());

            var summary = Connection.RetrieveStatistics();
            var newSummaries = summary.QuerySummaryList.Skip(beforeCount).ToArray();
            newSummaries.Length.Should().BeGreaterThan(0, "Expected new query summaries after executing batch");
            var last = newSummaries.Last();

            last.StatementCount.Should().BeGreaterOrEqualTo(3);

            // For SELECT coverage, expect only the SELECT (last) statement to have metrics collected
            if (last.Infos.Length >= last.StatementCount)
            {
                for (int i = 0; i < last.StatementCount; i++)
                {
                    var info = i < last.Infos.Length ? last.Infos[i] : null;
                    if (i == last.StatementCount - 1)
                    {
                        info.Should().NotBeNull();
                        info.Count.Should().BeGreaterThan(0);
                    }
                    else
                    {
                        info.Should().NotBeNull();
                        info.Count.Should().Be(0);
                    }
                }
            }
            else
            {
                var nonEmpty = last.Infos.Count(i => i != null && i.Count > 0);
                nonEmpty.Should().Be(1, "Expected exactly one statement to have metrics when coverage=Select");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }
}
