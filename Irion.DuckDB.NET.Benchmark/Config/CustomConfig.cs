using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace Irion.DuckDB.NET.Benchmark.Config
{
    internal class CustomConfig : ManualConfig
    {
        public CustomConfig()
        {
            // bring in default loggers/exporters/columns so output appears
            foreach (var logger in DefaultConfig.Instance.GetLoggers()) AddLogger(logger);
            foreach (var exporter in DefaultConfig.Instance.GetExporters()) AddExporter(exporter);
            foreach (var column in DefaultConfig.Instance.GetColumnProviders()) AddColumnProvider(column);
            foreach (var diagnoser in DefaultConfig.Instance.GetDiagnosers()) AddDiagnoser(diagnoser);

            var job = Job.Default.WithId("MyBaseline")
                                 .WithIterationCount(50)
                                 .WithRuntime(CoreRuntime.Core10_0)
                                 .WithStrategy(RunStrategy.Monitoring);

            AddJob(job);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
