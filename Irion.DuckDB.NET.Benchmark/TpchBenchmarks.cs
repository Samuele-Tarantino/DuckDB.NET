using BenchmarkDotNet.Attributes;
using DuckDB.NET.Data;
using DuckDB.NET.Data.Profiling;
using DuckDB.NET.Native;
using DuckDB.NET.Test.Helpers;
using Irion.DuckDB.NET.Benchmark.Config;
using Perfolizer.Mathematics.OutlierDetection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Irion.DuckDB.NET.Benchmark
{
    //[CPUUsageDiagnoser]
    [Outliers(OutlierMode.DontRemove)]
    [Config(typeof(CustomConfig))]
    public class TpchBenchmarks
    {
        private DuckDBConnection? mainConnection;
        private DuckDBCommand? mainCommand;
        // Per-iteration connection/command used for extension loading & secret creation
        private DuckDBConnection? iterationConnection;
        private DuckDBCommand? iterationCommand;
        private string? attachedDbPath;


        //[ParamsAllValues]
        [Params(true)]
        public bool MetricsEnabled { get; set; } = false;

        //[ParamsAllValues]
        [Params(DuckDBProfilingMode.Standard)]
        public DuckDBProfilingMode ProfilingMode { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (!NativeLibraryHelper.TryLoad())
            {
                Console.Error.WriteLine("native assembly not found");
                return;
            }

            // Initialize an in-memory DuckDB connection and attempt to install/load the tpch extension.
            mainConnection = new DuckDBConnection("DataSource=:memory:");
            mainConnection.Open();
            mainCommand = mainConnection.CreateCommand();
            try
            {
                mainCommand.CommandText = "INSTALL tpch;";
                mainCommand.ExecuteNonQuery();
                mainCommand.CommandText = "LOAD tpch;";
                mainCommand.ExecuteNonQuery();
                // Generate TPCH sf=1 dataset into the current database
                mainCommand.CommandText = "CALL dbgen(sf=1);";
                mainCommand.ExecuteNonQuery();
            }
            catch (Exception)
            {
                // If the extension isn't available, fall back to a simple generated dataset that approximates scale.
                mainCommand.CommandText = "CREATE TABLE IF NOT EXISTS lineitem (l_orderkey INTEGER, l_partkey INTEGER, l_quantity INTEGER);";
                mainCommand.ExecuteNonQuery();
                // Insert a moderate number of rows to simulate work (1k)
                mainCommand.CommandText = "BEGIN TRANSACTION;";
                mainCommand.ExecuteNonQuery();
                var rnd = new Random(42);
                for (int i = 0; i < 1000; i++)
                {
                    mainCommand.CommandText = $"INSERT INTO lineitem VALUES ({rnd.Next(1, 100000)}, {rnd.Next(1, 100000)}, {rnd.Next(1, 100)});";
                    mainCommand.ExecuteNonQuery();
                }

                mainCommand.CommandText = "COMMIT;";
                mainCommand.ExecuteNonQuery();
            }

            EnableProfiling(mainConnection);
        }

        private void EnableProfiling(DuckDBConnection connection)
        {
            if (MetricsEnabled)
            {
                var options = new ProfilingOptions
                {
                    Format = DuckDBProfilingFormat.NoOutput,
                    Mode = ProfilingMode, //DuckDBProfilingMode.Detailed,
                    Coverage = DuckDBProfilingCoverage.All,
                    EnabledMetrics = []
                };
                connection.EnableProfiling(options);
            }
        }

        private void ReadMetrics(DuckDBConnection connection)
        {
            if (MetricsEnabled)
            {
                var metrics = connection.RetrieveStatistics();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(attachedDbPath) && File.Exists(attachedDbPath))
                {
                    File.Delete(attachedDbPath);
                }
            }
            catch
            {
            }

            mainCommand?.Dispose();
            mainConnection?.Dispose();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Each iteration needs a fresh connection where the extension is installed and the secret created.
            iterationConnection = new DuckDBConnection("DataSource=:memory:");
            try
            {
                iterationConnection.Open();
                iterationCommand = iterationConnection.CreateCommand();

                // Install and load httpfs, then create a secret using the CREATE SECRET syntax.
                iterationCommand.CommandText = "INSTALL httpfs;";
                iterationCommand.ExecuteNonQuery();
                iterationCommand.CommandText = "LOAD httpfs;";
                iterationCommand.ExecuteNonQuery();

                EnableProfiling(iterationConnection);
            }
            catch
            {
                // Extension or CREATE SECRET not available on this environment; clean up the iteration resources
                // and leave iterationCommand null so the benchmark method can detect the unsupported case.
                iterationCommand?.Dispose();
                iterationConnection?.Dispose();
                iterationCommand = null;
                iterationConnection = null;
            }
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            iterationCommand?.Dispose();
            iterationConnection?.Dispose();
            iterationCommand = null;
            iterationConnection = null;
        }

        public static IEnumerable<int> Queries => Enumerable.Range(1, 22);

        [Benchmark]
        [ArgumentsSource(nameof(Queries))]
        public void RunTpchQuery(int query)
        {
            mainCommand!.CommandText = $"PRAGMA tpch({query});";
            mainCommand.ExecuteNonQuery();

            ReadMetrics(mainConnection);
        }

        [Benchmark]
        public void SimpleAggregation()
        {
            mainCommand!.CommandText = "SELECT l_partkey, SUM(l_quantity) as s FROM lineitem GROUP BY l_partkey LIMIT 100;";
            using var reader = mainCommand.ExecuteReader();
            while (reader.Read())
            { /* iterate */
            }

            ReadMetrics(mainConnection);

        }

        [Benchmark]
        public void AttachDatabaseAndQuery()
        {
            // create a temporary on-disk database and attach it
            attachedDbPath = Path.Combine(Path.GetTempPath(), $"duckdb_bench_{Guid.NewGuid():N}.db");
            mainCommand!.CommandText = $"ATTACH DATABASE '{attachedDbPath}' AS attached_db;";
            mainCommand.ExecuteNonQuery();

            ReadMetrics(mainConnection);

            // run a simple query against the attached database (it will be empty)
            mainCommand.CommandText = "SELECT COUNT(*) FROM attached_db.sqlite_master WHERE 1=0;";
            try
            {
                mainCommand.ExecuteScalar();

                ReadMetrics(mainConnection);

            }
            catch
            { /* ignore — attached DB may not have that table */
            }

            mainCommand.CommandText = "DETACH DATABASE attached_db;";
            mainCommand.ExecuteNonQuery();

            ReadMetrics(mainConnection);

            // cleanup file
            try
            {
                File.Delete(attachedDbPath);
            }
            catch
            {
            }

            attachedDbPath = null;
        }

        [Benchmark]
        public void LoadExtension()
        {
            // Each iteration needs a fresh connection where the extension is installed and the secret created.
            using var connection = new DuckDBConnection("DataSource=:memory:");

            connection.Open();
            using var command = connection.CreateCommand();

            // Install and load httpfs, then create a secret using the CREATE SECRET syntax.
            command.CommandText = "INSTALL httpfs;";
            command.ExecuteNonQuery();

            ReadMetrics(connection);

            command.CommandText = "LOAD httpfs;";
            command.ExecuteNonQuery();

            ReadMetrics(connection);

        }

        [Benchmark]
        public void CreateSecret()
        {
            // The extension must have been installed and the secret created during IterationSetup. The
            // benchmark itself only verifies the secret/extension is usable, keeping the measured work focused.
            if (iterationCommand is null)
            {
                // Extension or CREATE SECRET not supported in this iteration; skip measurable work.
                return;
            }

            // Use CREATE SECRET syntax to register fake credentials for the extension.
            // If the running DuckDB build does not support CREATE SECRET, an exception will be thrown and
            // we will treat this iteration as not supporting the extension.
            iterationCommand.CommandText = "CREATE SECRET s3_credentials (type s3, KEY_ID 'AKIAFAKE', SECRET 'fakeSecret');";
            iterationCommand.ExecuteNonQuery();

            ReadMetrics(iterationConnection);

            iterationCommand.CommandText = "SELECT 1;";
            _ = iterationCommand.ExecuteScalar();

            ReadMetrics(iterationConnection);

        }
    }
}