using DuckDB.NET.Data.Profiling;
using System.IO;

namespace DuckDB.NET.Data.Connection;

/// <summary>
/// Holds the connection count and DuckDBDatabase structure for a FileName
/// </summary>
internal class FileReference(string filename)
{
    public DuckDBDatabase? Database { get; internal set; }

    public string FileName { get; } = filename;

    public long ConnectionCount { get; private set; } //don't need a long, but it is slightly faster on 64 bit systems

    /// <summary>
    /// Gets a value indicating whether profiling is enabled for the current context. 
    /// Is used to ensure that the DuckDBDatabase is created with the correct profiling mode when a connection is opened on the same database
    /// </summary>
    public bool IsProfilingEnabled { get; internal set; }

    /// <summary>
    /// Gets the profiling options used to configure performance profiling behavior.
    /// </summary>
    public ProfilingOptions? ProfilingOptions { get; internal set; }

    public long Decrement()
    {
        return --ConnectionCount;
    }

    public long Increment()
    {
        return ++ConnectionCount;
    }

    public override string ToString() => $"{Path.GetFileName(FileName)}";
}