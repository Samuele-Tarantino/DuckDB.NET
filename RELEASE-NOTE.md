New features:

- Added support for DuckDB's `ProfilingInfo` feature, allowing retrieval of detailed query execution profiles.

Quick snippet — enable profiling, run a query, read summary, then disable/reset:

```csharp
// Enable profiling (in-memory JSON summary)
connection.EnableProfiling(new ProfilingOptions { Coverage = DuckDBProfilingCoverage.All, Format = DuckDBProfilingFormat.Json });

// Run a query
using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT 1;";
using var r = cmd.ExecuteReader();

// Retrieve summary
var summary = connection.RetrieveStatistics();
Console.WriteLine($"Queries collected: {summary.QueryCount}");

// Disable and reset
connection.DisableProfiling(true);
```