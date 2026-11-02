namespace DuckDB.NET.Native.Bridge;

/// <summary>
/// C API exported by the <c>bridge</c> extension (bridge_capi.hpp). The extension is linked inside the DuckDB shared
/// library, so these functions live in the same native library as the rest of the C API.
/// <para>
/// Connections must be <c>duckdb_connection</c> handles of this same library and must stay open for the whole call.
/// Strings are UTF-8 and are copied by the bridge. Each function returns <see cref="DuckDBState.Success"/> or
/// <see cref="DuckDBState.Error"/>; on error the message is written in the caller-owned <c>error</c> buffer
/// (always '\0'-terminated, truncated on a UTF-8 boundary, may be empty when <c>errorSize</c> is 0).
/// </para>
/// <para>
/// Shutdown order: first the Delt (the instance that reads), then streams/duplicates, then the Connectors.
/// </para>
/// </summary>
public static partial class BridgeMethods
{
    private const string DuckDbLibrary = "duckdb";

    /// <summary>Size of the error buffer used by the managed overloads.</summary>
    public const int DefaultErrorSize = 1024;

    /// <summary>
    /// Registers in the instance of <paramref name="connection"/> the DataSource <paramref name="name"/>, backed by the
    /// instance of <paramref name="connectorConnection"/> (must be a different instance). The bridge keeps that instance alive.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_register_datasource", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState RegisterDataSource(DuckDBNativeConnection connection, string name,
        DuckDBNativeConnection connectorConnection, Span<byte> error, int errorSize);

    /// <summary>Removes the DataSource <paramref name="name"/> from the instance of <paramref name="connection"/>.</summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_unregister_datasource", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState UnregisterDataSource(DuckDBNativeConnection connection, string name,
        Span<byte> error, int errorSize);

    /// <summary>
    /// Registers a link: a single SELECT (CTEs allowed, no parameters) executed on the DataSource
    /// <paramref name="dataSourceName"/>, read with <c>SELECT * FROM bridge_scan('&lt;linkName&gt;')</c>.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_register_link", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState RegisterLink(DuckDBNativeConnection connection, string linkName,
        string dataSourceName, string sql, Span<byte> error, int errorSize);

    /// <summary>
    /// Registers a MATERIALIZED link: <paramref name="sql"/> (SELECT or EXPLAIN) runs on <paramref name="txConnection"/>,
    /// a connection owned by the host that belongs to the DataSource instance and must stay open while the link is
    /// registered. Single use, one scan at a time; the bridge does not manage transactions.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_register_link_materialized", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState RegisterLinkMaterialized(DuckDBNativeConnection connection, string linkName,
        string dataSourceName, DuckDBNativeConnection txConnection, string sql, Span<byte> error, int errorSize);

    /// <summary>
    /// Registers an UNMATERIALIZED link on the view <paramref name="viewName"/> (1-3 parts, already qualified against
    /// the DataSource). Must be called before the <c>CREATE VIEW</c> on the Delt that uses the link.
    /// </summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_register_link_view", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState RegisterLinkView(DuckDBNativeConnection connection, string linkName,
        string dataSourceName, string viewName, Span<byte> error, int errorSize);

    /// <summary>Removes the link <paramref name="linkName"/>. Scans already started complete.</summary>
    [LibraryImport(DuckDbLibrary, EntryPoint = "bridge_unregister_link", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial DuckDBState UnregisterLink(DuckDBNativeConnection connection, string linkName,
        Span<byte> error, int errorSize);

    // Convenience overloads: allocate the error buffer and decode it.

    public static DuckDBState RegisterDataSource(DuckDBNativeConnection connection, string name,
        DuckDBNativeConnection connectorConnection, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = RegisterDataSource(connection, name, connectorConnection, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState UnregisterDataSource(DuckDBNativeConnection connection, string name, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = UnregisterDataSource(connection, name, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState RegisterLink(DuckDBNativeConnection connection, string linkName, string dataSourceName,
        string sql, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = RegisterLink(connection, linkName, dataSourceName, sql, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState RegisterLinkMaterialized(DuckDBNativeConnection connection, string linkName,
        string dataSourceName, DuckDBNativeConnection txConnection, string sql, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = RegisterLinkMaterialized(connection, linkName, dataSourceName, txConnection, sql, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState RegisterLinkView(DuckDBNativeConnection connection, string linkName,
        string dataSourceName, string viewName, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = RegisterLinkView(connection, linkName, dataSourceName, viewName, buffer, buffer.Length);
        error = DecodeError(buffer);
        return state;
    }

    public static DuckDBState UnregisterLink(DuckDBNativeConnection connection, string linkName, out string error)
    {
        Span<byte> buffer = stackalloc byte[DefaultErrorSize];
        var state = UnregisterLink(connection, linkName, buffer, buffer.Length);
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
