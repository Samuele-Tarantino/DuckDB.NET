using Dapper;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using DuckDB.NET.Test.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using static DuckDB.NET.Native.NativeMethods;

namespace DuckDB.NET.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!NativeLibraryHelper.TryLoad())
            {
                Console.Error.WriteLine("native assembly not found");
                return;
            }
            //NativeDebugResolver.Initialize();

            PrintVersion();


            //DapperSample();

            //AdoNetSamples();

            //LowLevelBindingsSample();

            //BulkDataLoad();

            //ParametersBinding();

            Test();
        }

        private static void PrintVersion()
        {
            using var con = new DuckDBConnection("data source=:memory:");
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "PRAGMA Version";

            var version = cmd.ExecuteScalar();
            Console.WriteLine("DuckDB version: {0}", version);
        }

        private static void DapperSample()
        {
            var connectionString = "Data Source=:memory:";
            using (var cn = new DuckDBConnection(connectionString))
            {
                cn.Open();

                Console.WriteLine("DuckDB version: {0}", cn.ServerVersion);

                cn.Execute("CREATE TABLE test (id INTEGER, name VARCHAR)");

                var query = cn.Query<Row>("SELECT * FROM test");
                Console.WriteLine("Initial count: {0}", query.Count());

                cn.Execute("INSERT INTO test (id,name) VALUES (123,'test')");

                query = cn.Query<Row>("SELECT * FROM test");

                foreach (var q in query)
                {
                    Console.WriteLine($"{q.Id} {q.Name}");
                }
            }
        }

        private static void AdoNetSamples()
        {
            if (File.Exists("file.db"))
            {
                File.Delete("file.db");
            }

            using var duckDBConnection = new DuckDBConnection("Data Source=file.db");
            duckDBConnection.Open();

            using var command = duckDBConnection.CreateCommand();
            command.CommandText = "CREATE TABLE integers(foo INTEGER, bar INTEGER);";
            var executeNonQuery = command.ExecuteNonQuery();

            command.CommandText = "INSERT INTO integers VALUES (3, 4), (5, 6), (7, NULL);";
            executeNonQuery = command.ExecuteNonQuery();

            command.CommandText = "Select count(*) from integers";
            var executeScalar = command.ExecuteScalar();

            command.CommandText = "SELECT foo, bar FROM integers";
            using var reader = command.ExecuteReader();
            PrintQueryResults(reader);

            var results = duckDBConnection.Query<FooBar>("SELECT foo, bar FROM integers");

            try
            {
                command.CommandText = "Not a valid Sql statement";
                var causesError = command.ExecuteNonQuery();
            }
            catch (DuckDBException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void LowLevelBindingsSample()
        {
            var result = Startup.DuckDBOpen((string)null, out var database);

            using (database)
            {
                result = Startup.DuckDBConnect(database, out var connection);
                using (connection)
                {
                    result = Query.DuckDBQuery(connection, "CREATE TABLE integers(foo INTEGER, bar INTEGER);", out _);
                    result = Query.DuckDBQuery(connection, "INSERT INTO integers VALUES (3, 4), (5, 6), (7, NULL);", out _);
                    result = Query.DuckDBQuery(connection, "SELECT foo, bar FROM integers", out var queryResult);

                    PrintQueryResults(queryResult);

                    // clean up
                    Query.DuckDBDestroyResult(ref queryResult);

                    result = PreparedStatements.DuckDBPrepare(connection, "INSERT INTO integers VALUES (?, ?)", out var insertStatement);

                    using (insertStatement)
                    {
                        result = PreparedStatements.DuckDBBindInt32(insertStatement, 1, 42); // the parameter index starts counting at 1!
                        result = PreparedStatements.DuckDBBindInt32(insertStatement, 2, 43);

                        result = PreparedStatements.DuckDBExecutePrepared(insertStatement, out _);
                    }


                    result = PreparedStatements.DuckDBPrepare(connection, "SELECT * FROM integers WHERE foo = ?", out var selectStatement);

                    using (selectStatement)
                    {
                        result = PreparedStatements.DuckDBBindInt32(selectStatement, 1, 42);

                        result = PreparedStatements.DuckDBExecutePrepared(selectStatement, out queryResult);
                    }

                    PrintQueryResults(queryResult);

                    // clean up
                    Query.DuckDBDestroyResult(ref queryResult);
                }
            }
        }

        private static void BulkDataLoad()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (File.Exists("file.db"))
            {
                File.Delete("file.db");
            }

            //using var connection = new DuckDBConnection("DataSource=:memory:");
            using var connection = new DuckDBConnection("Data Source=file.db");
            connection.Open();

            using (var duckDbCommand = connection.CreateCommand())
            {
                var table = "CREATE TABLE AppenderTest(i INTEGER, f FLOAT, d DOUBLE,c VARCHAR);";
                duckDbCommand.CommandText = table;
                duckDbCommand.ExecuteNonQuery();
            }

            var rows = 100000;
            using (var appender = connection.CreateAppender("AppenderTest"))
            {
                stopwatch.Start();
                for (var i = 0; i < rows; i++)
                {
                    var row = appender.CreateRow();
                    row
                        .AppendValue(i)
                        .AppendValue(Convert.ToSingle(i + 2))
                        .AppendValue(Convert.ToDouble(i + 4))
                        .AppendValue($"varchar {i.ToString()}")
                        .EndRow();
                }
                stopwatch.Stop();
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(1) as cnt FROM AppenderTest";
            using var reader1 = command.ExecuteReader();
            PrintQueryResults(reader1);
            command.CommandText = "SELECT * FROM AppenderTest limit 10";
            using var reader2 = command.ExecuteReader();
            PrintQueryResults(reader2);
            Console.WriteLine($"ElapsedMilliseconds {stopwatch.ElapsedMilliseconds}");
            Console.WriteLine($"connection state before {connection.State}");
            Console.WriteLine($"connection state after  {connection.State}");

        }

        private static void ParametersBinding()
        {

            using var duckDBConnection = new DuckDBConnection("Data Source=:memory:");
            duckDBConnection.Open();

            var testDicto = new Dictionary<string, string>
            {
                ["foo"] = "42",
                ["bar"] = "test"
            };
            var testList = new List<string> { "foo", "bar" };
            var testDictoList = new Dictionary<string, List<string>>
            {
                ["foo"] = testList
            };

            using var command = duckDBConnection.CreateCommand();
            command.CommandText = "CREATE TABLE TestTable (foo MAP(string, string), bar MAP(string, string[]), baz string[])";
            command.ExecuteNonQuery();

            command.CommandText = "INSERT INTO TestTable (foo, bar, baz) VALUES ($testDicto, $testDictoList, $testList)";
            command.Parameters.Add(new DuckDBParameter(testDicto));
            command.Parameters.Add(new DuckDBParameter(testDictoList));
            command.Parameters.Add(new DuckDBParameter(testList));
            command.ExecuteNonQuery();

            command.CommandText = "SELECT foo, bar, baz FROM TestTable";
            using var reader = command.ExecuteReader();
            PrintQueryResults(reader);
        }

        private static void Test()
        {

            using var con = new DuckDBConnection("data source=:memory:");
            con.Open();

            con.ProfilingEnabled = true;


            using var cmd = con.CreateCommand();

            cmd.CommandText = @"
            CALL enable_profiling(
                format := 'json',
                coverage := 'select',
                mode := 'standard',
                metrics := ['QUERY_NAME', 'LATENCY', 'OPERATOR_TIMING']
            );";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT a:1;";
            using var reader = cmd.ExecuteReader();

            var metrics = con.RetrieveStatistics();

            PrintQueryResults(reader);

        }

        private static void PrintQueryResults(DbDataReader queryResult)
        {
            for (var index = 0; index < queryResult.FieldCount; index++)
            {
                var column = queryResult.GetName(index);
                Console.Write($"{column} ");
            }

            Console.WriteLine();

            while (queryResult.Read())
            {
                for (int ordinal = 0; ordinal < queryResult.FieldCount; ordinal++)
                {
                    if (queryResult.IsDBNull(ordinal))
                    {
                        Console.WriteLine("NULL");
                        continue;
                    }
                    var val = queryResult.GetValue(ordinal);

                    Console.Write(FormatValue(val));

                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }

        private static void PrintQueryResults(DuckDBResult queryResult)
        {
            var columnCount = (int)Query.DuckDBColumnCount(ref queryResult);
            for (var index = 0; index < columnCount; index++)
            {
                var columnName = Query.DuckDBColumnName(ref queryResult, index);
                Console.Write($"{columnName} ");
            }

            Console.WriteLine();

            var rowCount = Query.DuckDBRowCount(ref queryResult);
            for (long row = 0; row < rowCount; row++)
            {
                for (long column = 0; column < columnCount; column++)
                {
                    var val = Types.DuckDBValueInt32(ref queryResult, column, row);
                    Console.Write(val);
                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }

        private static string FormatValue(object? value)
        {
            if (value is null)
            {
                return "NULL";
            }

            if (value is string s)
            {
                return s;
            }

            if (value is IDictionary dictionary)
            {
                var parts = new List<string>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    parts.Add($"{FormatValue(entry.Key)}:{FormatValue(entry.Value)}");
                }

                return "{" + string.Join(", ", parts) + "}";
            }

            if (value is IEnumerable enumerable)
            {
                var parts = enumerable.Cast<object?>().Select(FormatValue);
                return "[" + string.Join("|", parts) + "]";
            }

            return value.ToString() ?? string.Empty;
        }
    }

    class FooBar
    {
        public int Foo { get; set; }
        public int Bar { get; set; }
    }

    class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
