using DuckDB.NET.Data.Common;
using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Test.Helpers;
using System;
using System.Linq;
using System.Threading;

namespace DuckDB.NET.Test.Profiling;

public class ProfilingTests(DuckDBDatabaseFixture db) : DuckDBTestBase(db)
{

    [Fact]
    public void ProfilingSummariesContainCorrectStateAndMessageOnSuccess()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection(),
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();
            // Expect overall query to be successful
            last.State.Should().Be(DuckDBState.Success);
            last.Message.Should().BeNullOrEmpty();

            // Expect constituent statements to be successful as well
            if (last.StatementSummaries.Length > 0)
            {
                var stmt = last.StatementSummaries.Last();
                stmt.State.Should().Be(DuckDBState.Success);
                stmt.Message.Should().BeNullOrEmpty();
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void ProfilingSummary_ConversionsAreConsistent()
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
            Connection.ResetStatistics();

            // run two queries with measurable time
            Command.CommandText = "SELECT SUM(i) FROM range(200000) AS t(i);";
            using (var r = Command.ExecuteReader()) { }

            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThanOrEqualTo(1);

            // connection-level: wall-clock vs stored connection time (milliseconds)
            var wallConnMs = (summary.EndTime - summary.StartTime).TotalMilliseconds;
            Math.Abs(wallConnMs - summary.ConnectionTimeMilliseconds).Should().BeLessThan(250, "Connection time conversion should match wall-clock duration");

            // execution time (total) should equal summed query execution times
            var sumQueryExecMs = summary.QuerySummaryList.Sum(q => q.ExecutionTimeMilliseconds);
            Math.Abs(sumQueryExecMs - summary.ExecutionTimeMilliseconds).Should().BeLessThan(200, "Total execution time should match sum of query execution times");

            // per-query: check wall-clock vs measured execution time and statement sum
            foreach (var q in summary.QuerySummaryList)
            {
                var wallMs = (q.EndTime - q.StartTime).TotalMilliseconds;
                Math.Abs(wallMs - q.ExecutionTimeMilliseconds).Should().BeLessThan(250, "Query execution conversion should match wall-clock duration");

                var sumStmtMs = q.StatementSummaries.Sum(s => s.ExecutionTimeMilliseconds);
                Math.Abs(sumStmtMs - q.ExecutionTimeMilliseconds).Should().BeLessThan(200, "Query execution time should match sum of statement execution times");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void TimerUtils_ConversionsAreConsistent()
    {
        // measure a short delay using Stopwatch ticks and verify conversions
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        Thread.Sleep(120);
        long end = System.Diagnostics.Stopwatch.GetTimestamp();

        long elapsed = end - start;

        var msFromTimer = TimerUtils.TimerToMilliseconds(elapsed);
        var span = TimerUtils.TimerToTimeSpan(elapsed);

        // both conversions should be approximately the same
        Math.Abs(((long)span.TotalMilliseconds) - msFromTimer).Should().BeLessOrEqualTo(2);

        // and should be close to the real sleep duration
        msFromTimer.Should().BeGreaterOrEqualTo(100);
    }

    [Fact]
    public void Profiler_StartStop_ProducesConsistentTiming()
    {
        var options = new ProfilingOptions { Coverage = DuckDBProfilingCoverage.All };
        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            // run a query that takes measurable time and is visible through public RetrieveStatistics API
            Command.CommandText = "SELECT SUM(i) FROM range(200000) AS t(i);";
            using (var r = Command.ExecuteReader()) { }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();

            // execution time should be non-zero and reasonable
            last.ExecutionTimeMilliseconds.Should().BeGreaterThan(0);

            var wallMs = (last.EndTime - last.StartTime).TotalMilliseconds;

            // the wall-clock duration and the measured execution time should be reasonably close
            Math.Abs(wallMs - last.ExecutionTimeMilliseconds).Should().BeLessThan(200, "Timer conversion should be consistent with wall-clock duration");
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void QueryAndStatementStatusAndTimingWhenPrepareFails()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection(),
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            var id = Guid.NewGuid().ToString("N");
            var tbl = $"pfp_{id}";

            // batch where one statement will fail at prepare time (syntax error)
            Command.CommandText = $"CREATE TABLE {tbl}(i INTEGER); BAD SYNTAX HERE; SELECT 1;";

            try
            {
                using (var r = Command.ExecuteReader()) { do { } while (r.NextResult()); }
            }
            catch (Exception)
            {
                // swallow - prepare/execute is expected to throw
            }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();

            // Query-level state should reflect the failure
            last.State.Should().Be(DuckDBState.Error);
            last.Message.Should().NotBeNullOrWhiteSpace();

            // Timing at query-level should be well-formed
            last.ExecutionTimeMilliseconds.Should().BeGreaterOrEqualTo(0);
            (last.StartTime <= last.EndTime).Should().BeTrue("StartTime should be less than or equal to EndTime");

            last.StatementSummaries.Should().NotBeNull();
            last.StatementSummaries.Length.Should().Be(0, "No statements should be recorded when prepare fails");

        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void QueryAndStatementStatusAndTimingWhenStatementFails()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection(),
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            var id = Guid.NewGuid().ToString("N");
            var tbl = $"pf_{id}";

            // batch with multiple statements where the last one will fail
            Command.CommandText = $"CREATE TABLE {tbl}(i INTEGER); INSERT INTO {tbl} VALUES (1); SELECT i FROM {tbl}; SELECT * FROM __this_table_does_not_exist__;";

            try
            {
                using (var r = Command.ExecuteReader()) { do { } while (r.NextResult()); }
            }
            catch (Exception)
            {
                // swallow - we expect an error for the failing statement
            }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();

            // Query-level state should reflect the failure
            last.State.Should().Be(DuckDBState.Error);
            last.Message.Should().NotBeNullOrWhiteSpace();

            // Timing at query-level should be well-formed
            last.ExecutionTimeMilliseconds.Should().BeGreaterOrEqualTo(0);
            (last.StartTime <= last.EndTime).Should().BeTrue("StartTime should be less than or equal to EndTime");

            // Ensure at least one statement reports error and at least one reports success
            last.StatementSummaries.Should().NotBeNull();
            last.StatementSummaries.Length.Should().BeGreaterThan(0);

            var anyError = last.StatementSummaries.Any(i => i.State == DuckDBState.Error && !string.IsNullOrWhiteSpace(i.Message));
            anyError.Should().BeTrue("At least one statement should report an error state and message when one statement in a query fails");

            var anySuccess = last.StatementSummaries.Any(i => i.State == DuckDBState.Success);
            anySuccess.Should().BeTrue("At least one statement prior to the failing statement should have succeeded");

            // Check statement timing sanity
            foreach (var stmt in last.StatementSummaries)
            {
                stmt.ExecutionTimeMilliseconds.Should().BeGreaterOrEqualTo(0);
                (stmt.StartTime <= stmt.EndTime).Should().BeTrue("Statement StartTime should be less than or equal to EndTime");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void MetricsCollectedWhenOverMetricsThreshold()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName, DuckDBMetricType.CpuTime },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard,
            MetricsThreshold = 1 // very small threshold so a heavy query will exceed it
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            // run a heavier query to exceed the threshold
            // use DuckDB's range table function with a single argument (0..n-1)
            Command.CommandText = "SELECT SUM(i) FROM range(1000000) AS t(i);";
            using (var r = Command.ExecuteReader()) { }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();
            last.StatementSummaries.Should().NotBeNull();

            // When execution is over the metrics threshold, at least one statement should contain enabled metrics
            var hasMetrics = last.StatementSummaries.Any(i => i.Metrics.ContainsKey(DuckDBMetricType.QueryName) || i.Metrics.ContainsKey(DuckDBMetricType.CpuTime));
            hasMetrics.Should().BeTrue("Metrics should be collected when execution time exceeds MetricsThresholdMS");
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void MetricsNotCollectedWhenUnderMetricsThreshold()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection { DuckDBMetricType.QueryName },
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard,
            MetricsThreshold = 10000 // high threshold to prevent metrics collection for a short query
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            Command.CommandText = "SELECT 1;";
            using (var r = Command.ExecuteReader()) { }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();
            last.StatementSummaries.Should().NotBeNull();

            // When execution is under the metrics threshold, no metrics should be extracted
            last.StatementSummaries.All(i => i.Metrics.Count == 0).Should().BeTrue("Metrics should not be collected when execution time is below the configured MetricsThresholdMS");
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

    [Fact]
    public void ProfilingSummariesContainCorrectStateAndMessageOnError()
    {
        var options = new ProfilingOptions
        {
            Coverage = DuckDBProfilingCoverage.All,
            EnabledMetrics = new DuckDBMetricTypeCollection(),
            Format = DuckDBProfilingFormat.Json,
            Mode = DuckDBProfilingMode.Standard
        };

        Connection.EnableProfiling(options);

        try
        {
            Connection.ResetStatistics();

            // Execute a statement that will error
            Command.CommandText = "SELECT * FROM __this_table_does_not_exist__";
            try
            {
                using (var r = Command.ExecuteReader()) { }
            }
            catch (Exception)
            {
                // swallow - we expect an error to be thrown by the command execution
            }

            var summary = Connection.RetrieveStatistics();
            summary.QuerySummaryList.Length.Should().BeGreaterThan(0);

            var last = summary.QuerySummaryList.Last();
            // Overall query should be marked as error
            last.State.Should().Be(DuckDBState.Error);
            last.Message.Should().NotBeNullOrWhiteSpace();

            // Ensure at least one statement reports error state/message
            var anyError = last.StatementSummaries.Any(i => i.State == DuckDBState.Error && !string.IsNullOrWhiteSpace(i.Message));
            anyError.Should().BeTrue("At least one statement should report an error state and message");
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }

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
            var last = newSummaries.Last().StatementSummaries;

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
    public void EnableProfilingWithoutOptionsUsesDefaults()
    {
        // enable profiling with explicit empty options - should use defaults
        var defaultMetrics = DuckDBMetrics.DefaultMetrics.ToList();
        _ = defaultMetrics.Remove(DuckDBMetricType.QueryName);
        var options = new ProfilingOptions { Coverage = DuckDBProfilingCoverage.All, EnabledMetrics = new DuckDBMetricTypeCollection(defaultMetrics) };

        var con = new DuckDBConnection("data source=:memory:");
        con.Open();

        con.EnableProfiling(options);

        using var cmd = con.CreateCommand();

        try
        {
            // ensure clean start
            con.ResetStatistics();

            // run a simple query to produce profiling data
            cmd.CommandText = "SELECT 1;";
            using (var r = cmd.ExecuteReader()) { }

            // profiling should be enabled
            con.ProfilingEnabled.Should().BeTrue();

            var summary = con.RetrieveStatistics();
            summary.QueryCount.Should().BeGreaterThan(0);


            // enable profiling without passing any options - should reset to defaults if already enabled
            con.EnableProfiling();

            con.ResetStatistics();

            cmd.CommandText = "SET user = 'test';";
            using (var r = cmd.ExecuteReader()) { }

            // profiling should be enabled
            con.ProfilingEnabled.Should().BeTrue();

            summary = con.RetrieveStatistics();
            summary.QueryCount.Should().BeGreaterThan(0);

            // with default options, there should be no metrics collected for Set statements
            summary.QuerySummaryList.Last().StatementSummaries.Length.Should().Be(1);
            summary.QuerySummaryList.Last().StatementSummaries[0].Metrics.Count.Should().Be(0);
        }
        finally
        {
            con.DisableProfiling(true);
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

                var keys1 = new HashSet<DuckDBMetricType>(last1.StatementSummaries.SelectMany(i => i.Metrics.Keys));
                var keys2 = new HashSet<DuckDBMetricType>(last2.StatementSummaries.SelectMany(i => i.Metrics.Keys));

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

            var keysOrig = new HashSet<DuckDBMetricType>(lastOriginal.StatementSummaries.SelectMany(i => i.Metrics.Keys));
            var keysDup = new HashSet<DuckDBMetricType>(lastDup.StatementSummaries.SelectMany(i => i.Metrics.Keys));

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
            last.StatementSummaries.Should().NotBeNull();
            last.StatementSummaries.Length.Should().BeGreaterThan(0, "Expected some profiling info when metrics are enabled");

            // ensure at least one statement collected all requested metrics
            bool found = last.StatementSummaries.Any(summary => metricsToEnable.All(m => summary.Metrics.ContainsKey(m)));

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

            last.StatementSummaries.Should().NotBeNull();

            if (last.StatementSummaries.Length >= last.StatementCount)
            {
                for (int i = 0; i < last.StatementCount; i++)
                {
                    var info = i < last.StatementSummaries.Length ? last.StatementSummaries[i].Metrics : null;
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
                var nonEmpty = last.StatementSummaries.Count(i => i.Metrics.Count > 0);
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
            if (last.StatementSummaries.Length >= last.StatementCount)
            {
                for (int i = 0; i < last.StatementCount; i++)
                {
                    var info = i < last.StatementSummaries.Length ? last.StatementSummaries[i].Metrics : null;
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
                var nonEmpty = last.StatementSummaries.Count(i => i.Metrics.Count > 0);
                nonEmpty.Should().Be(1, "Expected exactly one statement to have metrics when coverage=Select");
            }
        }
        finally
        {
            Connection.DisableProfiling();
        }
    }
}
