using DuckDB.NET.Native;
using DuckDB.NET.Native.Bridge;

namespace Irion.DuckDB.NET.Test.Bridge;

/// <summary>
/// Tests of the bridge C API (<see cref="BridgeMethods"/>) against the native library shipped by the
/// Irion.DuckDb.Native.Bridge package. Delt and Connector are two separate in-memory instances.
/// </summary>
public sealed class BridgeMethodsTests : IDisposable
{
    private readonly DuckDBConnection _delt;
    private readonly DuckDBConnection _connector;

    public BridgeMethodsTests()
    {
        // A plain in-memory connection string opens a new, independent instance every time.
        _delt = new DuckDBConnection("DataSource=:memory:");
        _connector = new DuckDBConnection("DataSource=:memory:");
        _delt.Open();
        _connector.Open();

        Execute(_connector, "CREATE TABLE t AS SELECT range AS i, 'v' || range AS s FROM range(5)");
        Execute(_connector, "CREATE VIEW v AS SELECT i FROM t WHERE i >= 3");
    }

    // The Delt must be closed before the Connector.
    public void Dispose()
    {
        _delt.Dispose();
        _connector.Dispose();
    }

    [Fact]
    public void RegisterDataSource_is_listed_by_bridge_datasources()
    {
        Register("A").Should().Be(DuckDBState.Success);

        Scalar(_delt, "SELECT count(*) FROM bridge_datasources()").Should().Be(1L);
    }

    [Fact]
    public void RegisterLink_can_be_read_with_bridge_scan()
    {
        Register("A");

        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_t", "A", "SELECT i, s FROM t ORDER BY i", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT count(*) FROM bridge_scan('cl_t')").Should().Be(5L);
        Scalar(_delt, "SELECT sum(i)::BIGINT FROM bridge_scan('cl_t')").Should().Be(10L);
    }

    [Fact]
    public void RegisterLink_with_filter_returns_only_matching_rows()
    {
        Register("A");
        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_t", "A", "SELECT i, s FROM t", out _);

        Scalar(_delt, "SELECT count(*) FROM bridge_scan('cl_t') WHERE i > 1 AND s <> 'v4'").Should().Be(2L);
    }

    [Fact]
    public void RegisterLinkView_reads_a_view_of_the_connector()
    {
        Register("A");

        BridgeMethods.RegisterLinkView(_delt.NativeConnection, "cl_v", "A", "v", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT count(*) FROM bridge_scan('cl_v')").Should().Be(2L);
    }

    [Fact]
    public void RegisterLinkMaterialized_reads_on_the_host_transaction_connection()
    {
        Register("A");
        using var tx = _connector.Duplicate();
        tx.Open();
        Execute(tx, "BEGIN");
        Execute(tx, "CREATE TEMP TABLE tmp AS SELECT 42 AS x");

        BridgeMethods.RegisterLinkMaterialized(_delt.NativeConnection, "cl_m", "A", tx.NativeConnection, "SELECT x FROM tmp", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT x FROM bridge_scan('cl_m')").Should().Be(42);

        // The link must be gone before the transaction connection is closed.
        BridgeMethods.UnregisterLink(_delt.NativeConnection, "cl_m", out _).Should().Be(DuckDBState.Success);
        Execute(tx, "ROLLBACK");
    }

    [Fact]
    public void RegisterLink_on_unknown_datasource_fails_with_bridge_message()
    {
        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_x", "missing", "SELECT 1", out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:").And.Contain("missing");
    }

    [Fact]
    public void RegisterLink_twice_is_rejected_case_insensitively()
    {
        Register("A");
        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_t", "A", "SELECT 1 AS x", out _).Should().Be(DuckDBState.Success);

        BridgeMethods.RegisterLink(_delt.NativeConnection, "CL_T", "A", "SELECT 2 AS x", out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:");
    }

    [Fact]
    public void RegisterLink_with_invalid_sql_fails_and_registers_nothing()
    {
        Register("A");

        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_bad", "A", "SELECT * FROM no_such_table", out var error)
            .Should().Be(DuckDBState.Error);
        error.Should().Contain("bridge:");

        var act = () => Scalar(_delt, "SELECT count(*) FROM bridge_scan('cl_bad')");
        act.Should().Throw<DuckDBException>();
    }

    [Fact]
    public void RegisterDataSource_with_the_same_instance_is_rejected()
    {
        BridgeMethods.RegisterDataSource(_delt.NativeConnection, "self", _delt.NativeConnection, out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:");
    }

    [Fact]
    public void UnregisterLink_makes_the_link_unreadable()
    {
        Register("A");
        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_t", "A", "SELECT i FROM t", out _);

        BridgeMethods.UnregisterLink(_delt.NativeConnection, "cl_t", out var error)
            .Should().Be(DuckDBState.Success, error);

        var act = () => Scalar(_delt, "SELECT count(*) FROM bridge_scan('cl_t')");
        act.Should().Throw<DuckDBException>();
    }

    [Fact]
    public void UnregisterDataSource_removes_it_from_bridge_datasources()
    {
        Register("A");

        BridgeMethods.UnregisterDataSource(_delt.NativeConnection, "A", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT count(*) FROM bridge_datasources()").Should().Be(0L);
    }

    [Fact]
    public void Raw_overload_fills_the_caller_buffer_with_a_terminated_message()
    {
        Span<byte> buffer = stackalloc byte[256];

        var state = BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_x", "missing", "SELECT 1", buffer, buffer.Length);

        state.Should().Be(DuckDBState.Error);
        BridgeMethods.DecodeError(buffer).Should().StartWith("Invalid Input Error: bridge:");
    }

    [Fact]
    public void Raw_overload_truncates_the_message_to_the_buffer_size()
    {
        Span<byte> buffer = stackalloc byte[16];

        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_x", "missing", "SELECT 1", buffer, buffer.Length);

        buffer.IndexOf((byte)0).Should().BeInRange(1, 15);
    }

    [Fact]
    public void DecodeError_stops_at_the_first_terminator()
    {
        BridgeMethods.DecodeError("abc\0def"u8).Should().Be("abc");
        BridgeMethods.DecodeError("abc"u8).Should().Be("abc");
        BridgeMethods.DecodeError(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Names_and_values_are_passed_as_utf8()
    {
        Register("Fonte_è");

        BridgeMethods.RegisterLink(_delt.NativeConnection, "cl_è", "Fonte_è", "SELECT 'ü' AS c", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT c FROM bridge_scan('cl_è')").Should().Be("ü");
    }

    private DuckDBState Register(string name)
    {
        var state = BridgeMethods.RegisterDataSource(_delt.NativeConnection, name, _connector.NativeConnection, out var error);
        state.Should().Be(DuckDBState.Success, error);
        return state;
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
