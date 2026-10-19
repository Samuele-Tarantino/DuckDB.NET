namespace DuckDB.NET.Test;

public class StatementValidatorTests(DuckDBDatabaseFixture db) : DuckDBTestBase(db)
{
    private sealed class StopBatchException() : Exception("validator stop");

    private static Action<DuckDBStatementType> Recording(List<DuckDBStatementType> seen) => seen.Add;

    private List<string> TablesLike(string prefix)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = $"SELECT table_name FROM information_schema.tables WHERE table_name LIKE '{prefix}%' ORDER BY table_name";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static void Consume(DuckDBDataReader reader)
    {
        do
        {
            while (reader.Read())
            {
            }
        } while (reader.NextResult());
    }

    // The values must match the native duckdb_statement_type: a shifted enum would let LOAD/INSTALL through or block valid statements.
    [Theory]
    [InlineData("SELECT 1", DuckDBStatementType.Select)]
    [InlineData("INSERT INTO TypeProbe VALUES (1)", DuckDBStatementType.Insert)]
    [InlineData("UPDATE TypeProbe SET a = 2", DuckDBStatementType.Update)]
    [InlineData("DELETE FROM TypeProbe", DuckDBStatementType.Delete)]
    [InlineData("EXPLAIN SELECT 1", DuckDBStatementType.Explain)]
    [InlineData("CREATE TABLE TypeProbeNew(a INTEGER)", DuckDBStatementType.Create)]
    [InlineData("ALTER TABLE TypeProbe ADD COLUMN b INTEGER", DuckDBStatementType.Alter)]
    [InlineData("DROP TABLE TypeProbe", DuckDBStatementType.Drop)]
    [InlineData("BEGIN TRANSACTION", DuckDBStatementType.Transaction)]
    [InlineData("COPY TypeProbe TO 'typeprobe.csv'", DuckDBStatementType.Copy)]
    [InlineData("ANALYZE", DuckDBStatementType.Vacuum)] // DuckDB parses ANALYZE as a VACUUM statement
    [InlineData("SET threads = 1", DuckDBStatementType.Set)]
    [InlineData("PRAGMA disable_progress_bar", DuckDBStatementType.Pragma)]
    [InlineData("VACUUM", DuckDBStatementType.Vacuum)]
    [InlineData("CALL pragma_version()", DuckDBStatementType.Call)]
    [InlineData("ATTACH ':memory:' AS TypeProbeDb", DuckDBStatementType.Attach)]
    [InlineData("DETACH TypeProbeDb", DuckDBStatementType.Detach)]
    [InlineData("INSTALL mysql FROM core", DuckDBStatementType.Load)]
    [InlineData("FORCE INSTALL mysql FROM core", DuckDBStatementType.Load)]
    [InlineData("LOAD mysql", DuckDBStatementType.Load)]
    [InlineData("UPDATE EXTENSIONS", DuckDBStatementType.UpdateExtensions)]
    [InlineData("MERGE INTO TypeProbe t USING (SELECT 1 AS a) s ON t.a = s.a WHEN MATCHED THEN DELETE", DuckDBStatementType.MergeInto)]
    public void ValidatorReceivesTheNativeStatementType(string statement, DuckDBStatementType expected)
    {
        using var setup = Connection.CreateCommand();
        setup.CommandText = "CREATE OR REPLACE TABLE TypeProbe(a INTEGER); ATTACH IF NOT EXISTS ':memory:' AS TypeProbeDb;";
        setup.ExecuteNonQuery();

        DuckDBStatementType? seen = null;
        Command.CommandText = statement;
        // Stopping at the validator keeps statements with side effects (INSTALL, COPY, DROP, ...) from really running.
        Command.StatementValidator = type =>
        {
            seen = type;
            throw new StopBatchException();
        };

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<StopBatchException>();

        seen.Should().Be(expected);
    }

    [Fact]
    public void ValidatorIsCalledOncePerStatementInOrder()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 1; SELECT 2; CREATE TABLE OrderProbe(a INTEGER); INSERT INTO OrderProbe VALUES (1); DROP TABLE OrderProbe;";
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();

        seen.Should().Equal(
            DuckDBStatementType.Select,
            DuckDBStatementType.Select,
            DuckDBStatementType.Create,
            DuckDBStatementType.Insert,
            DuckDBStatementType.Drop);
    }

    [Fact]
    public void ValidatorRunsBeforeTheStatementItValidatesAndAfterThePreviousOnes()
    {
        var tablesWhenValidating = new List<int>();
        Command.CommandText = "CREATE TABLE TimingProbe1(a INTEGER); CREATE TABLE TimingProbe2(a INTEGER); CREATE TABLE TimingProbe3(a INTEGER);";
        Command.StatementValidator = _ => tablesWhenValidating.Add(TablesLike("TimingProbe").Count);

        Command.ExecuteNonQuery();

        tablesWhenValidating.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void DependentBatchExecutesWhenEveryStatementIsValidated()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "CREATE OR REPLACE TABLE DependentProbe AS SELECT 1 AS a; INSERT INTO DependentProbe(a) VALUES (2); SELECT a FROM DependentProbe ORDER BY a;";
        Command.StatementValidator = Recording(seen);

        using var reader = Command.ExecuteReader();
        var rows = new List<int>();
        do
        {
            while (reader.Read())
            {
                rows.Add(reader.GetInt32(0));
            }
        } while (reader.NextResult());

        seen.Should().Equal(DuckDBStatementType.Create, DuckDBStatementType.Insert, DuckDBStatementType.Select);
        rows.Should().Equal(1, 2);
    }

    [Fact]
    public void BatchesThatDependOnSchemaOrAttachedDatabaseExecuteStatementByStatement()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText =
            "CREATE SCHEMA DepSchema; CREATE TABLE DepSchema.t(a INTEGER); CREATE VIEW DepSchema.v AS SELECT a FROM DepSchema.t; " +
            "ALTER TABLE DepSchema.t ADD COLUMN b INTEGER; INSERT INTO DepSchema.t VALUES (1, 2); SELECT a FROM DepSchema.v;";
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();

        seen.Should().Equal(
            DuckDBStatementType.Create,
            DuckDBStatementType.Create,
            DuckDBStatementType.Create,
            DuckDBStatementType.Alter,
            DuckDBStatementType.Insert,
            DuckDBStatementType.Select);
        TablesLike("v").Should().Contain("v");
    }

    [Fact]
    public void ValidatorThrowingOnTheFirstStatementExecutesNothing()
    {
        Command.CommandText = "CREATE TABLE FirstStops1(a INTEGER); CREATE TABLE FirstStops2(a INTEGER);";
        Command.StatementValidator = _ => throw new StopBatchException();

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<StopBatchException>();

        TablesLike("FirstStops").Should().BeEmpty();
    }

    [Fact]
    public void ValidatorThrowingInTheMiddleKeepsPreviousStatementsAndSkipsTheRest()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "CREATE TABLE MiddleStops1(a INTEGER); INSTALL mysql FROM core; CREATE TABLE MiddleStops3(a INTEGER);";
        Command.StatementValidator = type =>
        {
            seen.Add(type);
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<StopBatchException>();

        seen.Should().Equal(DuckDBStatementType.Create, DuckDBStatementType.Load);
        TablesLike("MiddleStops").Should().Equal("MiddleStops1");
    }

    [Fact]
    public void ValidatorThrowingOnTheLastStatementIsNotSwallowed()
    {
        Command.CommandText = "CREATE TABLE LastStops1(a INTEGER); LOAD mysql;";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<StopBatchException>();

        TablesLike("LastStops").Should().Equal("LastStops1");
    }

    [Fact]
    public void ConnectionStaysUsableAfterTheValidatorStoppedABatch()
    {
        Command.CommandText = "SELECT 1; LOAD mysql; SELECT 3;";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<StopBatchException>();

        using var next = Connection.CreateCommand();
        next.CommandText = "SELECT 42";
        next.ExecuteScalar().Should().Be(42);
    }

    [Fact]
    public void ValidatorIsAppliedToExecuteReader()
    {
        Command.CommandText = "SELECT 1; LOAD mysql; SELECT 3;";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c =>
        {
            using var reader = c.ExecuteReader();
            Consume(reader);
        }).Should().Throw<StopBatchException>();
    }

    [Fact]
    public void ValidatorIsAppliedToExecuteScalar()
    {
        Command.CommandText = "LOAD mysql";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c => c.ExecuteScalar()).Should().Throw<StopBatchException>();
    }

    [Fact]
    public void ValidatorIsAppliedToExecuteArrowStream()
    {
        Command.CommandText = "LOAD mysql; SELECT 1";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new StopBatchException();
            }
        };

        Command.Invoking(c => c.ExecuteArrowStream()).Should().Throw<StopBatchException>();
    }

    [Fact]
    public void ValidatorIsAppliedToExecuteNonQueryWithParameters()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT ?1::integer; SELECT ?1::integer + 1";
        Command.Parameters.Add(new DuckDBParameter(41));
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();

        seen.Should().Equal(DuckDBStatementType.Select, DuckDBStatementType.Select);
    }

    [Fact]
    public void ValidatorIsCalledAgainWhenTheCommandIsExecutedAgain()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 1; SELECT 2";
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();
        Command.ExecuteNonQuery();

        seen.Should().HaveCount(4);
    }

    [Fact]
    public void ValidatorIsNotCalledWhenTheBatchCannotBeParsed()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 1; this is not sql";
        Command.StatementValidator = Recording(seen);

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<DuckDBException>();

        seen.Should().BeEmpty();
    }

    [Fact]
    public void ValidatorDoesNotSeeTheStatementThatFailsToPrepare()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 1; SELECT * FROM TableThatDoesNotExist; SELECT 3;";
        Command.StatementValidator = Recording(seen);

        Command.Invoking(c => c.ExecuteNonQuery()).Should().Throw<DuckDBException>()
            .Where(e => e.Message.Contains("TableThatDoesNotExist"));

        seen.Should().Equal(DuckDBStatementType.Select);
    }

    [Fact]
    public void ValidatorSeesTheBandingCommentPrefixAsPartOfTheFirstStatement()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "/*##START_QB##{\"a\": \"b; c\"}##END_QB##*/CREATE TABLE BandingProbe(a INTEGER); INSERT INTO BandingProbe VALUES (1);";
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();

        seen.Should().Equal(DuckDBStatementType.Create, DuckDBStatementType.Insert);
    }

    [Fact]
    public void ExtensionKeywordsInsideStringsAndCommentsAreNotExtensionStatements()
    {
        var seen = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 'INSTALL mysql; LOAD mysql'; -- INSTALL mysql\nSELECT 1; /* LOAD mysql; */ SELECT 2;";
        Command.StatementValidator = Recording(seen);

        Command.ExecuteNonQuery();

        seen.Should().OnlyContain(type => type == DuckDBStatementType.Select);
    }

    [Fact]
    public void WithoutValidatorTheBatchBehavesAsBefore()
    {
        Command.CommandText = "CREATE OR REPLACE TABLE NoValidatorProbe AS SELECT 1 AS a; INSERT INTO NoValidatorProbe VALUES (2); SELECT count(*) FROM NoValidatorProbe;";

        Command.ExecuteScalar().Should().Be(2L);
    }
}
