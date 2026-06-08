using System.Collections;

namespace DuckDB.NET.Test.Parameters;

public class MapParameterTests(DuckDBDatabaseFixture db) : DuckDBTestBase(db)
{
    public sealed record MapTypeCase(
        string Name,
        string DuckDbType,
        Type ClrType,
        Func<Faker, object> Generator,
        Func<object, object> Normalize);

    private static readonly MapTypeCase[] ScalarTypeCases =
    [
        new("bool", "bool", typeof(bool), faker => faker.Random.Bool(), value => value),
        new("sbyte", "tinyint", typeof(sbyte), faker => faker.Random.SByte(), value => value),
        new("short", "SmallInt", typeof(short), faker => faker.Random.Short(), value => value),
        new("int", "int", typeof(int), faker => faker.Random.Int(), value => value),
        new("long", "BigInt", typeof(long), faker => faker.Random.Long(), value => value),
        new("hugeint", "HugeInt", typeof(BigInteger), faker => BigInteger.Subtract(DuckDBHugeInt.HugeIntMaxValue, faker.Random.Int(min: 0)), value => value),
        new("byte", "UTinyInt", typeof(byte), faker => faker.Random.Byte(), value => value),
        new("ushort", "USmallInt", typeof(ushort), faker => faker.Random.UShort(), value => value),
        new("uint", "UInteger", typeof(uint), faker => faker.Random.UInt(), value => value),
        new("ulong", "UBigInt", typeof(ulong), faker => faker.Random.ULong(), value => value),
        new("float", "Float", typeof(float), faker => faker.Random.Float(), value => value),
        new("double", "Double", typeof(double), faker => faker.Random.Double(), value => value),
        new("decimal", "Decimal(38, 28)", typeof(decimal), faker => faker.Random.Decimal(), value => value),
        new("guid", "UUID", typeof(Guid), faker => faker.Random.Uuid(), value => value),
        new("datetime", "Date", typeof(DateTime), faker => faker.Date.Past().Date, NormalizeDateValue),
        new("datetimeoffset", "TimeTZ", typeof(DateTimeOffset), faker => GetRandomTimeTz(faker), value => value),
        new("string", "String", typeof(string), faker => faker.Random.Utf16String(), value => value),
        new("interval", "Interval", typeof(TimeSpan), faker =>
        {
            var timespan = faker.Date.Timespan();
            return TimeSpan.FromTicks(timespan.Ticks - timespan.Ticks % 10);
        }, value => value),
        new("duckdbdateonly", "Date", typeof(DuckDBDateOnly), faker => (DuckDBDateOnly)faker.Date.Past().Date, NormalizeDateValue),
        new("duckdbtimeonly", "Time", typeof(DuckDBTimeOnly), faker => (DuckDBTimeOnly)faker.Date.Past(), NormalizeTimeValue),
        new("dateonly", "Date", typeof(DateOnly), faker => DateOnly.FromDateTime(faker.Date.Past().Date), NormalizeDateValue),
        new("timeonly", "Time", typeof(TimeOnly), faker =>
        {
            var dateTime = faker.Date.Past();
            return new TimeOnly(dateTime.TimeOfDay.Ticks - dateTime.TimeOfDay.Ticks % 10);
        }, NormalizeTimeValue)
    ];

    public static IEnumerable<object[]> ScalarKeyValueCombinations =>
        from keyCase in ScalarTypeCases
        from valueCase in ScalarTypeCases
        select new object[] { keyCase, valueCase };

    [Theory]
    [MemberData(nameof(ScalarKeyValueCombinations))]
    public void CanBindMapForEveryScalarKeyAndValueCombination(MapTypeCase keyCase, MapTypeCase valueCase)
    {
        var map = CreateMap(keyCase, valueCase);

        Action act = () => InsertSelectMap(keyCase, valueCase, map);
        act.Should().NotThrow($"key={keyCase.Name}, value={valueCase.Name}");
    }

    [Fact]
    public void CanBindMapWithNestedListValues()
    {
        var value = new Dictionary<string, List<int>>
        {
            ["one"] = new List<int> { 1, 2, 0 },
            ["two"] = new List<int> { 3, 0, 5 }
        };

        InsertSelectMap("String", "INTEGER[]", value);
    }

    [Fact]
    public void CanBindMapWithNestedMapValues()
    {
        var value = new Dictionary<int, Dictionary<string, TimeOnly>>
        {
            [1] = new Dictionary<string, TimeOnly>
            {
                ["a"] = new TimeOnly(11, 22, 33),
                ["b"] = new TimeOnly(9, 5, 2)
            },
            [2] = new Dictionary<string, TimeOnly>
            {
                ["x"] = new TimeOnly(6, 30, 45)
            }
        };

        InsertSelectMap("INTEGER", "MAP(String, Time)", value);
    }

    private void InsertSelectMap(string keyDuckDbType, string valueDuckDbType, IDictionary map)
    {
        Command.Parameters.Clear();
        Command.CommandText = $"CREATE OR REPLACE TABLE ParameterMapTest (a MAP({keyDuckDbType}, {valueDuckDbType}));";
        Command.ExecuteNonQuery();

        Command.CommandText = "INSERT INTO ParameterMapTest (a) VALUES ($map);";
        Command.Parameters.Add(new DuckDBParameter(map));
        Command.ExecuteNonQuery();

        Command.Parameters.Clear();
        Command.CommandText = "SELECT a FROM ParameterMapTest;";

        using var reader = Command.ExecuteReader();
        reader.Read().Should().BeTrue();

        var value = reader.GetValue(0);
        value.Should().BeEquivalentTo(map);
    }

    private void InsertSelectMap(MapTypeCase keyCase, MapTypeCase valueCase, IDictionary map)
    {
        Command.Parameters.Clear();
        Command.CommandText = $"CREATE OR REPLACE TABLE ParameterMapTest (a MAP({keyCase.DuckDbType}, {valueCase.DuckDbType}));";
        Command.ExecuteNonQuery();

        Command.CommandText = "INSERT INTO ParameterMapTest (a) VALUES ($map);";
        Command.Parameters.Add(new DuckDBParameter(map));
        Command.ExecuteNonQuery();

        Command.Parameters.Clear();
        Command.CommandText = "SELECT a FROM ParameterMapTest;";

        using var reader = Command.ExecuteReader();
        reader.Read().Should().BeTrue();

        var actual = (IDictionary)reader.GetValue(0);

        var expectedEntries = map.Keys.Cast<object>()
            .Select(key => new
            {
                Key = keyCase.Normalize(key),
                Value = valueCase.Normalize(map[key]!)
            });

        var actualEntries = actual.Keys.Cast<object>()
            .Select(key => new
            {
                Key = keyCase.Normalize(key),
                Value = valueCase.Normalize(actual[key]!)
            });

        actualEntries.Should().BeEquivalentTo(expectedEntries);
    }

    private IDictionary CreateMap(MapTypeCase keyCase, MapTypeCase valueCase)
    {
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyCase.ClrType, valueCase.ClrType);
        var map = (IDictionary)Activator.CreateInstance(dictionaryType)!;

        object? key = null;
        var attempts = 0;

        while (key is null && attempts < 10)
        {
            key = keyCase.Generator(Faker);
            attempts++;
        }

        key.Should().NotBeNull();
        var value = valueCase.Generator(Faker);
        map.Add(key!, value);

        return map;
    }

    private static DateTimeOffset GetRandomTimeTz(Faker faker)
    {
        var dateTime = faker.Date.Between(DateTime.Now.AddYears(-100), DateTime.Now.AddYears(100));

        if (dateTime.Hour < 1)
        {
            dateTime = dateTime.AddHours(1);
        }

        dateTime = dateTime.AddTicks(-dateTime.Ticks % 10);

        return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), TimeSpan.FromHours(1));
    }

    private static object NormalizeDateValue(object value)
    {
        return value switch
        {
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DuckDBDateOnly duckDBDateOnly => (DateOnly)duckDBDateOnly,
            DateOnly dateOnly => dateOnly,
            _ => value
        };
    }

    private static object NormalizeTimeValue(object value)
    {
        return value switch
        {
            DuckDBTimeOnly duckDBTimeOnly => (TimeOnly)duckDBTimeOnly,
            TimeOnly timeOnly => timeOnly,
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            _ => value
        };
    }
}