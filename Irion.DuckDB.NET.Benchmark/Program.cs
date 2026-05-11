using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Irion.DuckDB.NET.Benchmark.Config;
using System.Diagnostics;
using System.Linq;

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
            var type = typeof(TpchBenchmarks_Baseline);

            BenchmarkRunner.Run(type, config, args);
        }
    }
}
