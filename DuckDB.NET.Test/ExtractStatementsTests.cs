namespace DuckDB.NET.Test;

public class ExtractStatementsTests(DuckDBDatabaseFixture db) : DuckDBTestBase(db)
{
    [Fact]
    public void MultipleInsertsIntoTable()
    {
        Command.CommandText = "CREATE TABLE Test(foo INTEGER, bar INTEGER);";
        Command.ExecuteNonQuery();

        Command.CommandText = "Insert into Test (foo, bar) values (1,2); Insert into Test (foo, bar) values (3,4);";
        Command.ExecuteNonQuery().Should().Be(2);

        Command.CommandText = "Select * from Test";
        var dataReader = Command.ExecuteReader();

        dataReader.Read();
        dataReader.GetInt32(0).Should().Be(1);
        dataReader.GetInt32(1).Should().Be(2);
        
        dataReader.Read();
        dataReader.GetInt32(0).Should().Be(3);
        dataReader.GetInt32(1).Should().Be(4);
    }

    [Fact]
    public void WrongCommandThrowsException()
    {
        Command.CommandText = "error";
        
        Command.Invoking(cmd => cmd.ExecuteNonQuery()).Should()
               .Throw<DuckDBException>().Where(e => e.Message.Contains("syntax error at or near"));
    }

    [Fact]
    public void NotExistingTableThrowsException()
    {
        Command.CommandText = "Select 2; Select 1 from dummy";
        
        Command.Invoking(cmd => cmd.ExecuteNonQuery()).Should()
               .Throw<DuckDBException>().Where(e => e.Message.Contains("Table with name dummy does not exist"));
    }

    [Fact]
    public void MissingParametersThrowsException()
    {
        Command.CommandText = "Select ?1::integer; Select ?1::integer, ?2::integer";
        Command.Parameters.Add(new DuckDBParameter(42));

        var dataReader = Command.ExecuteReader();

        dataReader.Invoking(reader => reader.NextResult()).Should()
            .Throw<InvalidOperationException>().Where(e => e.Message.Contains("Invalid number of parameters. Expected 2, got 1"));
    }

    [Theory]
    [InlineData("INSTALL mysql FROM core", DuckDBStatementType.Load)]
    [InlineData("FORCE INSTALL mysql FROM core", DuckDBStatementType.Load)]
    [InlineData("LOAD mysql", DuckDBStatementType.Load)]
    [InlineData("UPDATE EXTENSIONS", DuckDBStatementType.UpdateExtensions)]
    public void StatementValidator_ReceivesTheNativeTypeOfExtensionStatements(string statement, DuckDBStatementType expected)
    {
        DuckDBStatementType? seen = null;
        Command.CommandText = statement;
        // Throwing keeps the extension command from really running (it would need network access).
        Command.StatementValidator = type =>
        {
            seen = type;
            throw new InvalidOperationException("stop");
        };

        Command.Invoking(cmd => cmd.ExecuteNonQuery()).Should().Throw<InvalidOperationException>();

        seen.Should().Be(expected);
    }

    [Fact]
    public void StatementValidator_WhenKeywordsAreOnlyInStringsOrComments_DoesNotSeeExtensionStatements()
    {
        var validated = new List<DuckDBStatementType>();
        Command.CommandText = "SELECT 'INSTALL mysql; LOAD mysql'; -- INSTALL mysql\nSELECT 1;";
        Command.StatementValidator = validated.Add;

        Command.ExecuteNonQuery();

        validated.Should().Equal(DuckDBStatementType.Select, DuckDBStatementType.Select);
    }

    private const string DependentBatch =
        "CREATE OR REPLACE TABLE BatchDependent AS SELECT 1 AS a; INSERT INTO BatchDependent(a) VALUES (2); SELECT a FROM BatchDependent ORDER BY a;";

    [Fact]
    public void StatementValidator_WhenStatementDependsOnPreviousOne_ValidatesStatementByStatementAndExecutesTheBatch()
    {
        var validated = new List<DuckDBStatementType>();
        Command.CommandText = DependentBatch;
        Command.StatementValidator = validated.Add;

        using var reader = Command.ExecuteReader();

        var rows = new List<int>();
        do
        {
            while (reader.Read())
            {
                rows.Add(reader.GetInt32(0));
            }
        } while (reader.NextResult());

        validated.Should().Equal(DuckDBStatementType.Create, DuckDBStatementType.Insert, DuckDBStatementType.Select);
        rows.Should().Equal(1, 2);
    }

    [Fact]
    public void StatementValidator_WhenItThrows_StopsTheBatchKeepingThePreviousStatementsEffects()
    {
        Command.CommandText = "CREATE TABLE ValidatorStops(a INTEGER); INSTALL mysql FROM core; CREATE TABLE ValidatorNeverCreated(a INTEGER);";
        Command.StatementValidator = type =>
        {
            if (type == DuckDBStatementType.Load)
            {
                throw new InvalidOperationException("extension commands are disabled");
            }
        };

        Command.Invoking(cmd => cmd.ExecuteNonQuery()).Should()
            .Throw<InvalidOperationException>().WithMessage("extension commands are disabled");

        using var check = Connection.CreateCommand();
        check.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_name IN ('ValidatorStops', 'ValidatorNeverCreated')";
        using var reader = check.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        tables.Should().Equal("ValidatorStops");
    }
}