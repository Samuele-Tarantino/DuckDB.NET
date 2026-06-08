using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Irion.DuckDB.NET.Benchmark.Config;
using System;
using System.Diagnostics;
using System.IO;

namespace Irion.DuckDB.NET.Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ManualConfig config;

            if (!Debugger.IsAttached)
            {
                config = new CustomConfig();
            }
            else
            {
                config = ManualConfig.Create(new DebugInProcessConfig());
            }

            //clean 1.5.2 benchmark
            //var type = typeof(TpchBenchmarks_Baseline);
            var type = typeof(TpchBenchmarks);

            config.ArtifactsPath = Path.Combine("BenchmarkDotNet.Artifacts", $"{type.Name}_{DateTime.Now:yyyyMMdd-HHmmss}");

            BenchmarkRunner.Run(type, config, args);
        }
    }
}
