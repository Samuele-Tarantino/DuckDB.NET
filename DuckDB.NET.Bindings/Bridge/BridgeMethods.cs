namespace DuckDB.NET.Native.Bridge;

/// <summary>Outcome of the last write from an instance, as reported by <c>bridge_write_stats</c>.</summary>
public enum BridgeWriteOutcome
{
    /// <summary>No write counted on that connection (or one is still running).</summary>
    None = 0,
    Committed = 1,
    /// <summary>Rolled back: the other values are the numbers reached before the rollback.</summary>
    RolledBack = 2,
}

/// <summary>Metrics of the last write from an instance into the Delt (see <c>BridgeMethods.WriteStats</c>).</summary>
/// <param name="Rows">Rows written.</param>
/// <param name="Bytes">Bytes of data copied by the sink: uncompressed, not the size on disk.</param>
/// <param name="DurationMs">Duration of the write in milliseconds.</param>
/// <param name="Outcome">Outcome of the write.</param>
public readonly record struct BridgeWriteStats(ulong Rows, ulong Bytes, long DurationMs, BridgeWriteOutcome Outcome);

/// <summary>
/// C API exported by the <c>bridge</c> extension (bridge_capi.hpp). The extension is linked inside the DuckDB shared
/// library, so these functions live in the same native library as the rest of the C API.
/// <para>
/// An instance (a separate DuckDB instance prepared by the host, with its own secrets, extensions and ATTACHes) is
/// registered on the Delt with <see cref="RegisterInstance(DuckDBNativeConnection, string, DuckDBNativeConnection, Span{byte}, int)"/>.
/// From then on any connection of the Delt reads it in SQL with <c>ATTACH 'bridge:&lt;name&gt;' AS alias</c>
/// (optionally <c>(READ_ONLY)</c>) and <c>alias.object</c>; <c>CONNECT alias</c> / <c>DISCONNECT</c> run a batch on the
/// instance session (with DuckDB 2.0.0 use <c>duckdb_query</c>, not prepare, between them; from 2.0.1 prepare works too). See the bridge HOST-GUIDE.
/// </para>
/// <para>
/// Connections must be <c>duckdb_connection</c> handles of this same library and must stay open for the whole call.
/// Strings are UTF-8 and are copied by the bridge. Each function returns <see cref="DuckDBState.Success"/> or
/// <see cref="DuckDBState.Error"/>; on error the message is written in the caller-owned <c>error</c> buffer
/// (always '\0'-terminated, truncated on a UTF-8 boundary, may be empty when <c>errorSize</c> is 0).
/// </para>
/// <para>
/// Shutdown order: DETACH and unregister first, then the Delt, then the instances.
/// </para>
/// </summary>
public static partial class BridgeMethods
{
    private const string DuckDbLibrary = "duckdb";

    /// <summary>Size of the error buffer used by the managed overloads.</summary>
    public const int DefaultErrorSize = 1024;

    /// <summary>
    /// Registers in the Delt of <paramref name="connection"/> the instance <paramref name="name"/>, backed by the
    /// DuckDB instance of <paramref name="instanceConnection"/> (must be a different instance from the Delt; names are
    /// case-insensitive and cannot be registered twice). The bridge keeps that instance alive: the host may close its
    /// own connection afterwards. There is no per-connection grant: registering is enough.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_register_instance", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState RegisterInstance(DuckDBNativeConnection connection, string name,
        DuckDBNativeConnection instanceConnection, Span<byte> error, int errorSize);

    /// <summary>
    /// Removes the instance <paramref name="name"/> from the Delt of <paramref name="connection"/>. Catalogs attached
    /// to that instance stop working (with a clear error).
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_unregister_instance", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState UnregisterInstance(DuckDBNativeConnection connection, string name,
        Span<byte> error, int errorSize);

    /// <summary>
    /// Outcome of the last write from an instance (<c>CREATE TABLE ... AS</c> / <c>INSERT ... SELECT</c> towards a
    /// <c>bridge</c> catalog) executed on <paramref name="connection"/>, the instance connection that ran the statement
    /// (not the alias and not the Delt connection). Call it right after the statement, even if it failed: every statement
    /// started on the connection resets the numbers, and a statement that does not write through the sink (including a
    /// CTAS/INSERT pushed whole to the Delt) gives <see cref="BridgeWriteOutcome.None"/>. Not cumulative.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_write_stats", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState WriteStats(DuckDBNativeConnection connection, out ulong rows, out ulong bytes,
        out long durationMs, out int outcome, Span<byte> error, int errorSize);

    // Convenience overloads: allocate the error buffer and decode it.

    public static DuckDBState RegisterInstance(DuckDBNativeConnection connection, string name,
        DuckDBNativeConnection instanceConnection, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = RegisterInstance(connection, name, instanceConnection, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState UnregisterInstance(DuckDBNativeConnection connection, string name, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = UnregisterInstance(connection, name, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    /// <summary>
    /// Outcome of the last write from an instance (see the raw overload), as a <see cref="BridgeWriteStats"/>.
    /// </summary>
    public static DuckDBState WriteStats(DuckDBNativeConnection connection, out BridgeWriteStats stats, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = WriteStats(connection, out var rows, out var bytes, out var durationMs, out var outcome, buffer, buffer.Length);
        stats = new BridgeWriteStats(rows, bytes, durationMs, (BridgeWriteOutcome)outcome);
        error = DecodeError(buffer);
        return state;
    }

    /// <summary>Decodes a '\0'-terminated UTF-8 error buffer.</summary>
    public static string DecodeError(ReadOnlySpan<byte> buffer)
    {
        var end = buffer.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? buffer : buffer[..end]);
    }
}
