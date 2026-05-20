using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Test.Helpers;

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
            var last = newSummaries.Last().Infos;

            // check that enabled metrics appear in at least one statement's metrics
            var containsQueryName = last.Any(i => i.Metrics.ContainsKey(DuckDBMetricType.QueryName));
            var containsCpuTime = last.Any(i => i.Metrics.ContainsKey(DuckDBMetricType.CpuTime));

            containsQueryName.Should().BeTrue();
            containsCpuTime.Should().BeTrue();
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public async Task FileBackedConnectionSharesProfilingStateWhenEnabledOnFirst()
    {
        // create a physical file-backed database
        using var dbInfo = DisposableFile.GenerateInTemp("db");

        // open first connection and enable profiling
        await using (var conn1 = new DuckDBConnection(dbInfo.ConnectionString))
        {
            conn1.Open();

            var options = new ProfilingOptions
            {
                Coverage = DuckDBProfilingCoverage.All,
                EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
                Format = DuckDBProfilingFormat.Json,
                Mode = DuckDBProfilingMode.Standard
            };

            conn1.EnableProfiling(options);

            // run a query to ensure profiling collects something
            using var cmd1 = conn1.CreateCommand();
            cmd1.CommandText = "SELECT 1;";
            using (var r = cmd1.ExecuteReader()) { }

            // open a second connection to the same file WITHOUT enabling profiling explicitly
            await using (var conn2 = new DuckDBConnection(dbInfo.ConnectionString))
            {
                conn2.Open();

                // conn2 should have profiling enabled because it shares the file-backed DB state
                conn2.ProfilingEnabled.Should().BeTrue();

                // run a query on conn2 and ensure it has its own statistics recorded
                using var cmd2 = conn2.CreateCommand();
                cmd2.CommandText = "SELECT 2;";
                using (var r = cmd2.ExecuteReader()) { }

                var s2 = conn2.RetrieveStatistics();
                s2.QueryCount.Should().BeGreaterThan(0);
            }

            conn1.DisableProfiling(true);
        }
    }

    [Fact]
    public void InMemoryDuplicateHasIndependentStatistics()
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
            // ensure clean start
            Connection.ResetStatistics();

            // run a query on the parent
            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            var parentSummary = Connection.RetrieveStatistics();
            parentSummary.QueryCount.Should().BeGreaterThanOrEqualTo(1);
            var parentCount = parentSummary.QueryCount;

            // create a duplicate connection and run a separate query
            using var dup = Connection.Duplicate();
            dup.Open();
            using var dupCmd = dup.CreateCommand();
            dupCmd.CommandText = "SELECT 2;";
            using (var r = dupCmd.ExecuteReader()) { }

            var dupSummary = dup.RetrieveStatistics();
            // the duplicate should have its own stats with only the query it ran
            dupSummary.QueryCount.Should().Be(1);

            // parent stats should not include duplicate's query
            var parentAfter = Connection.RetrieveStatistics();
            parentAfter.QueryCount.Should().Be(parentCount);
        }
        finally
        {
            Connection.DisableProfiling(true);
        }
    }

    [Fact]
    public async Task FileBackedDuplicateReturnsSameMetricsAsOriginal()
    {
        using var dbInfo = DisposableFile.GenerateInTemp("db");

        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName, DuckDBMetricType.CpuTime, DuckDBMetricType.Latency },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        await using (var conn1 = new DuckDBConnection(dbInfo.ConnectionString))
        {
            conn1.Open();
            conn1.EnableProfiling(options);

            using (var c = conn1.CreateCommand())
            {
                c.CommandText = "SELECT 1;";
                using var r = c.ExecuteReader();
            }

            await using (var conn2 = new DuckDBConnection(dbInfo.ConnectionString))
            {
                conn2.Open();
                // conn2 should have profiling enabled implicitly for file-backed DB when conn1 enabled it
                conn2.ProfilingEnabled.Should().BeTrue();

                using (var c2 = conn2.CreateCommand())
                {
                    c2.CommandText = "SELECT 2;";
                    using var r2 = c2.ExecuteReader();
                }

                var s1 = conn1.RetrieveStatistics();
                var s2 = conn2.RetrieveStatistics();

                s1.QuerySummaryList.Length.Should().BeGreaterThan(0, "Expected profiling summaries for original connection");
                s2.QuerySummaryList.Length.Should().BeGreaterThan(0, "Expected profiling summaries for duplicate connection");

                var last1 = s1.QuerySummaryList.Last();
                var last2 = s2.QuerySummaryList.Last();

                var keys1 = new HashSet<DuckDBMetricType>(last1.Infos.SelectMany(i => i.Metrics.Keys));
                var keys2 = new HashSet<DuckDBMetricType>(last2.Infos.SelectMany(i => i.Metrics.Keys));

                // strict check: both sets of metric keys must match
                keys1.Should().BeEquivalentTo(keys2);
            }

            conn1.DisableProfiling(true);
        }
    }

    [Fact]
    public void InMemoryDuplicateReturnsSameMetricsAsOriginal()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName, DuckDBMetricType.CpuTime, DuckDBMetricType.Latency },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            // run a query on the original
            Command.CommandText = "SELECT 10;";
            using (var r = Command.ExecuteReader()) { }

            // duplicate in-memory and open
            using var dup = Connection.Duplicate();
            dup.Open();

            // run a query on duplicate
            using var cdup = dup.CreateCommand();
            cdup.CommandText = "SELECT 20;";
            using (var r = cdup.ExecuteReader()) { }

            var sOriginal = Connection.RetrieveStatistics();
            var sDup = dup.RetrieveStatistics();

            sOriginal.QuerySummaryList.Length.Should().BeGreaterThan(0, "Expected profiling summaries for original connection");
            sDup.QuerySummaryList.Length.Should().BeGreaterThan(0, "Expected profiling summaries for duplicate connection");

            var lastOriginal = sOriginal.QuerySummaryList.Last();
            var lastDup = sDup.QuerySummaryList.Last();

            var keysOrig = new HashSet<DuckDBMetricType>(lastOriginal.Infos.SelectMany(i => i.Metrics.Keys));
            var keysDup = new HashSet<DuckDBMetricType>(lastDup.Infos.SelectMany(i => i.Metrics.Keys));

            // strict check: both sets of metric keys must match
            keysOrig.Should().BeEquivalentTo(keysDup);
        }
        finally
        {
            Connection.DisableProfiling(true);
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
            bool found = last.Infos.Any(summary => metricsToEnable.All(m => summary.Metrics.ContainsKey(m)));

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
                    var info = i < last.Infos.Length ? last.Infos[i].Metrics : null;
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
                // If Metrics length doesn't map 1:1 to statements, at least ensure exactly one statement collected metrics
                var nonEmpty = last.Infos.Count(i => i.Metrics.Count > 0);
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
                    var info = i < last.Infos.Length ? last.Infos[i].Metrics : null;
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
                var nonEmpty = last.Infos.Count(i => i.Metrics.Count > 0);
                nonEmpty.Should().Be(1, "Expected exactly one statement to have metrics when coverage=Select");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }
}
