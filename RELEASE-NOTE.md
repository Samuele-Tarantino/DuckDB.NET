New functionality

- ProfilingInfo: collect detailed per-query execution profiles and metrics (timing, operator breakdown, runtime statistics).

How to enable

1. Enable profiling on an open `DuckDBConnection` with `ProfilingOptions`:

```csharp
connection.EnableProfiling(new ProfilingOptions {
  Coverage = DuckDBProfilingCoverage.All,
  Format = DuckDBProfilingFormat.Json,
  Mode = DuckDBProfilingMode.Standard,
  //OutputPath = "profiling-output.json",           // optional path for generated files
  // EnabledMetrics = new DuckDBMetricTypeCollection(...), // optional: pick specific metrics
  MetricsThreshold = 5                         // optional: ignore measurements under X ms
});
```

2. Run queries as usual using `CreateCommand()` / `ExecuteReader()` / `ExecuteNonQuery()`.

Retrieve summaries

- Call `connection.RetrieveStatistics()` to obtain the profiling summary. The returned object includes:
  - `QueryCount` and `QuerySummaryList` — overall counts and per-query summaries
  - Per-query timings, operator breakdowns, and collected metrics

Example (read summary):

```csharp
var summary = connection.RetrieveStatistics();
Console.WriteLine($"Collected {summary.QueryCount} queries");
foreach (var q in summary.QuerySummaryList ?? Enumerable.Empty<ProfilingQuerySummary>())
{
  var firstStatement = q.StatementSummaries != null && q.StatementSummaries.Length > 0
    ? q.StatementSummaries[0].Metrics[MetricsType.QueryName]
    : "(no statement)";

  Console.WriteLine($"Query: {firstStatement} — Duration: {q.ExecutionTimeMilliseconds} ms");
}
```

Disable and reset

- To stop profiling and clear collected data:

```csharp
connection.DisableProfiling(reset: true);
```

Notes

- `MetricsThreshold` helps filter noise by ignoring very short measurements.
- `QuerySummaryList` may be null when no queries were captured; use `?? Enumerable.Empty<QuerySummary>()` when enumerating.

ProfilingOptions fields

- `Coverage` (enum: `DuckDBProfilingCoverage`): controls which queries/operators are profiled. Valid values:
  - `Select` — profile SELECT queries (default for fine-grained sampling of reads).
  - `All` — profile all query kinds and collect operator-level details.

- `Format` (enum: `DuckDBProfilingFormat`): output format for summaries. Valid values:
  - `QueryTree` — tree-style query plan output (human-readable; use this for a "Text" style summary).
  - `Json` — structured JSON suitable for storage and programmatic analysis.
  - `QueryTreeOptimizer` — query-tree output focused on optimizer phases.
  - `NoOutput` — disable textual/report output while still collecting metrics.

- `Mode` (enum: `DuckDBProfilingMode`): optional mode selector controlling standard vs detailed captures:
  - `Standard`, `Detailed`, `All` — choose the level of internal detail captured.


- `MetricsThreshold` (int): optional threshold in milliseconds; measurements shorter than this are ignored to reduce noise.
- `OutputPath` (string): optional file path for query plan output.
- `EnabledMetrics` (DuckDBMetricTypeCollection): optional collection of metric types to collect; omit to use the library defaults.
