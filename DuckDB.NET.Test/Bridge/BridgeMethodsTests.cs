using DuckDB.NET.Native.Bridge;

namespace DuckDB.NET.Test.Bridge;

/// <summary>
/// Tests of the bridge C API (<see cref="BridgeMethods"/>) against the native library shipped by the
/// Irion.DuckDb.Native.Bridge package. Delt and instance A are two separate in-memory instances; A is read from
/// the Delt with <c>ATTACH 'bridge:A'</c>.
/// </summary>
public sealed class BridgeMethodsTests : IDisposable
{
    private readonly DuckDBConnection _delt;
    private readonly DuckDBConnection _instance;

    public BridgeMethodsTests()
    {
        // A plain in-memory connection string opens a new, independent instance every time.
        _delt = new DuckDBConnection("DataSource=:memory:");
        _instance = new DuckDBConnection("DataSource=:memory:");
        _delt.Open();
        _instance.Open();

        Execute(_instance, "CREATE TABLE t AS SELECT range AS i, 'v' || range AS s FROM range(5)");
        Execute(_instance, "CREATE VIEW v AS SELECT i FROM t WHERE i >= 3");
    }

    // Deregister first, then close the Delt, then the instance.
    public void Dispose()
    {
        foreach (var name in new[] { "A", "isoldata", "Fonte_è" })
        {
            BridgeMethods.UnregisterInstance(_delt.NativeConnection, name, out _);
            BridgeMethods.UnregisterInstance(_instance.NativeConnection, name, out _);
        }

        _delt.Dispose();
        _instance.Dispose();
    }

    [Fact]
    public void RegisterInstance_is_listed_by_bridge_instances()
    {
        Register("A");

        Scalar(_delt, "SELECT count(*) FROM bridge_instances()").Should().Be(1L);
    }

    [Fact]
    public void Attach_reads_a_table_of_the_instance()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");

        Scalar(_delt, "SELECT count(*) FROM dba.t").Should().Be(5L);
        Scalar(_delt, "SELECT sum(i)::BIGINT FROM dba.t").Should().Be(10L);
    }

    [Fact]
    public void Attach_with_filter_and_projection_returns_the_expected_rows()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");

        Scalar(_delt, "SELECT count(*) FROM dba.t WHERE i > 1 AND s <> 'v4'").Should().Be(2L);
        Scalar(_delt, "SELECT s FROM dba.t WHERE i = 3").Should().Be("v3");
    }

    [Fact]
    public void Attach_reads_a_view_of_the_instance()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");

        Scalar(_delt, "SELECT count(*) FROM dba.v").Should().Be(2L);
    }

    [Fact]
    public void Attach_can_be_joined_with_a_local_table_of_the_delt()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");
        Execute(_delt, "CREATE TABLE local_t AS SELECT range AS i FROM range(3)");

        Scalar(_delt, "SELECT count(*) FROM dba.t a JOIN local_t l ON a.i = l.i").Should().Be(3L);
    }

    [Fact]
    public void Attach_read_only_rejects_writes()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba (READ_ONLY)");

        var act = () => Execute(_delt, "INSERT INTO dba.t VALUES (99, 'x')");

        act.Should().Throw<DuckDBException>();
        Scalar(_instance, "SELECT count(*) FROM t").Should().Be(5L);
    }

    [Fact]
    public void Attach_of_an_unregistered_instance_fails_with_bridge_message()
    {
        var act = () => Execute(_delt, "ATTACH 'bridge:missing' AS dbx");

        act.Should().Throw<DuckDBException>().Which.Message.Should().Contain("bridge:").And.Contain("missing");
    }

    [Fact]
    public void RegisterInstance_twice_is_rejected_case_insensitively()
    {
        Register("A");

        BridgeMethods.RegisterInstance(_delt.NativeConnection, "a", _instance.NativeConnection, out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:");
    }

    [Fact]
    public void RegisterInstance_with_the_same_instance_as_the_delt_is_rejected()
    {
        BridgeMethods.RegisterInstance(_delt.NativeConnection, "self", _delt.NativeConnection, out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:");
    }

    [Fact]
    public void UnregisterInstance_removes_it_from_bridge_instances()
    {
        Register("A");

        BridgeMethods.UnregisterInstance(_delt.NativeConnection, "A", out var error)
            .Should().Be(DuckDBState.Success, error);

        Scalar(_delt, "SELECT count(*) FROM bridge_instances()").Should().Be(0L);
    }

    [Fact]
    public void UnregisterInstance_makes_attached_catalogs_unreadable()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");
        Scalar(_delt, "SELECT count(*) FROM dba.t").Should().Be(5L);

        BridgeMethods.UnregisterInstance(_delt.NativeConnection, "A", out var error)
            .Should().Be(DuckDBState.Success, error);

        var act = () => Scalar(_delt, "SELECT count(*) FROM dba.t");
        act.Should().Throw<DuckDBException>();
    }

    [Fact]
    public void UnregisterInstance_of_an_unknown_name_fails()
    {
        BridgeMethods.UnregisterInstance(_delt.NativeConnection, "missing", out var error)
            .Should().Be(DuckDBState.Error);

        error.Should().Contain("bridge:");
    }

    [Fact]
    public void Connect_batch_runs_on_the_instance_session_with_duckdb_query()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");

        // Between CONNECT and DISCONNECT every statement goes to the session of the instance and must use
        // duckdb_query (not prepare): DuckDBCommand cannot be used there.
        RawQuery(_delt, "CONNECT dba");
        RawQuery(_delt, "BEGIN");
        RawQuery(_delt, "CREATE TEMP TABLE tmp AS SELECT 42 AS x");
        RawQuery(_delt, "DISCONNECT");

        // The session (and its TEMP table) outlives DISCONNECT and is visible to reads of the same connection.
        Scalar(_delt, "SELECT x FROM dba.tmp").Should().Be(42);

        RawQuery(_delt, "CONNECT dba");
        RawQuery(_delt, "ROLLBACK");
        RawQuery(_delt, "DISCONNECT");
    }

    [Fact]
    public void Connect_batch_works_with_DuckDBCommand_since_duckdb_2_0_1()
    {
        Register("A");
        Execute(_delt, "ATTACH 'bridge:A' AS dba");

        // DuckDB 2.0.0 failed here (PREPARE succeeded but the prepared statement was not registered, duckdb#27269):
        // from 2.0.1 the prepare under CONNECT goes straight to the session and DISCONNECT is not forwarded.
        Execute(_delt, "CONNECT dba");
        Execute(_delt, "BEGIN");
        Execute(_delt, "CREATE TEMP TABLE tmp AS SELECT 7 AS x");
        Scalar(_delt, "SELECT 1").Should().Be(1);
        Execute(_delt, "DISCONNECT");

        Scalar(_delt, "SELECT x FROM dba.tmp").Should().Be(7);

        Execute(_delt, "CONNECT dba");
        Execute(_delt, "ROLLBACK");
        Execute(_delt, "DISCONNECT");
    }

    [Fact]
    public void Instance_can_write_to_the_delt_when_the_delt_is_registered_on_it()
    {
        Register("A");
        BridgeMethods.RegisterInstance(_instance.NativeConnection, "isoldata", _delt.NativeConnection, out var error)
            .Should().Be(DuckDBState.Success, error);

        // Run on the connection of A, without CONNECT: the bridge writes the rows into the Delt.
        Execute(_instance, "ATTACH 'bridge:isoldata' AS isoldata");
        Execute(_instance, "CREATE TABLE isoldata.main.out AS SELECT i, s FROM t");
        Execute(_instance, "INSERT INTO isoldata.main.out SELECT i + 100, s FROM t");

        Scalar(_delt, "SELECT count(*) FROM main.out").Should().Be(10L);
        Scalar(_delt, "SELECT max(i) FROM main.out").Should().Be(104L);
    }

    [Fact]
    public void WriteStats_reports_rows_bytes_and_outcome_of_the_last_write_from_the_instance()
    {
        Register("A");
        BridgeMethods.RegisterInstance(_instance.NativeConnection, "isoldata", _delt.NativeConnection, out _);
        Execute(_instance, "ATTACH 'bridge:isoldata' AS isoldata");

        Execute(_instance, "CREATE TABLE isoldata.main.out AS SELECT i, s FROM t");

        BridgeMethods.WriteStats(_instance.NativeConnection, out var stats, out var error)
            .Should().Be(DuckDBState.Success, error);
        stats.Outcome.Should().Be(BridgeWriteOutcome.Committed);
        stats.Rows.Should().Be(5);
        stats.Bytes.Should().BeGreaterThan(0);
        stats.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void WriteStats_is_reset_by_the_next_statement_on_the_connection()
    {
        Register("A");
        BridgeMethods.RegisterInstance(_instance.NativeConnection, "isoldata", _delt.NativeConnection, out _);
        Execute(_instance, "ATTACH 'bridge:isoldata' AS isoldata");
        Execute(_instance, "CREATE TABLE isoldata.main.out AS SELECT i, s FROM t");

        Scalar(_instance, "SELECT 1");

        BridgeMethods.WriteStats(_instance.NativeConnection, out var stats, out _).Should().Be(DuckDBState.Success);
        stats.Outcome.Should().Be(BridgeWriteOutcome.None);
        stats.Rows.Should().Be(0);
    }

    [Fact]
    public void WriteStats_is_not_cumulative_between_writes()
    {
        Register("A");
        BridgeMethods.RegisterInstance(_instance.NativeConnection, "isoldata", _delt.NativeConnection, out _);
        Execute(_instance, "ATTACH 'bridge:isoldata' AS isoldata");
        Execute(_instance, "CREATE TABLE isoldata.main.out AS SELECT i, s FROM t");

        Execute(_instance, "INSERT INTO isoldata.main.out SELECT i + 100, s FROM t WHERE i < 2");

        BridgeMethods.WriteStats(_instance.NativeConnection, out var stats, out _);
        stats.Outcome.Should().Be(BridgeWriteOutcome.Committed);
        stats.Rows.Should().Be(2);
    }

    [Fact]
    public void WriteStats_without_any_write_reports_no_outcome()
    {
        BridgeMethods.WriteStats(_instance.NativeConnection, out var stats, out var error)
            .Should().Be(DuckDBState.Success, error);

        stats.Outcome.Should().Be(BridgeWriteOutcome.None);
    }

    [Fact]
    public void WriteStats_raw_overload_returns_the_same_values()
    {
        Register("A");
        BridgeMethods.RegisterInstance(_instance.NativeConnection, "isoldata", _delt.NativeConnection, out _);
        Execute(_instance, "ATTACH 'bridge:isoldata' AS isoldata");
        Execute(_instance, "CREATE TABLE isoldata.main.out AS SELECT i, s FROM t");
        Span<byte> buffer = stackalloc byte[256];

        var state = BridgeMethods.WriteStats(_instance.NativeConnection, out var rows, out var bytes, out _, out var outcome, buffer, buffer.Length);

        state.Should().Be(DuckDBState.Success);
        rows.Should().Be(5);
        bytes.Should().BeGreaterThan(0);
        outcome.Should().Be((int)BridgeWriteOutcome.Committed);
    }

    [Fact]
    public void Raw_overload_fills_the_caller_buffer_with_a_terminated_message()
    {
        Span<byte> buffer = stackalloc byte[256];

        var state = BridgeMethods.UnregisterInstance(_delt.NativeConnection, "missing", buffer, buffer.Length);

        state.Should().Be(DuckDBState.Error);
        BridgeMethods.DecodeError(buffer).Should().StartWith("Invalid Input Error: bridge:");
    }

    [Fact]
    public void Raw_overload_truncates_the_message_to_the_buffer_size()
    {
        Span<byte> buffer = stackalloc byte[16];

        BridgeMethods.UnregisterInstance(_delt.NativeConnection, "missing", buffer, buffer.Length);

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
    public void Instance_names_are_passed_as_utf8()
    {
        Register("Fonte_è");
        Execute(_delt, "ATTACH 'bridge:Fonte_è' AS dba_utf8");

        Scalar(_delt, "SELECT count(*) FROM dba_utf8.t").Should().Be(5L);
    }

    private void Register(string name)
    {
        BridgeMethods.RegisterInstance(_delt.NativeConnection, name, _instance.NativeConnection, out var error)
            .Should().Be(DuckDBState.Success, error);
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

    // duckdb_query on the native connection (the only way to run statements between CONNECT and DISCONNECT).
    private static void RawQuery(DuckDBConnection connection, string sql)
    {
        var state = NativeMethods.Query.DuckDBQuery(connection.NativeConnection, sql, out var result);
        try
        {
            state.Should().Be(DuckDBState.Success, NativeMethods.Query.DuckDBResultError(ref result));
        }
        finally
        {
            NativeMethods.Query.DuckDBDestroyResult(ref result);
        }
    }
}
