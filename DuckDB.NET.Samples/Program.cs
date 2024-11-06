using Dapper;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using DuckDB.NET.Test.Helpers;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            //DapperSample();

            //AdoNetSamples();

            //AdoNetBlobSample();
            //AdoNetDateTimeSample();
            AdoNetDuckDBBulkSamples();
            //AdoNetSqlBulkSamples();

            //LowLevelBindingsSample();
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
            var reader = command.ExecuteReader();
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

        /// <summary>
        /// SELECT '\xAA\xAB\xAC'::BLOB;
        /// </summary>
        private static void AdoNetBlobSample() {
            if (File.Exists("file.db")) {
                File.Delete("file.db");
            }

            using var duckDBConnection = new DuckDBConnection("Data Source=:memory:");
            duckDBConnection.Open();

            using var cmd = duckDBConnection.CreateCommand();
            cmd.CommandText = "SELECT 'AA'::BLOB;";
            using var reader = cmd.ExecuteReader();

            reader.Read();

            var type = reader.GetFieldType(0);
            var value = reader.GetValue(0);
            var value1 = reader.GetFieldValue<byte[]>(0);
            var value2 = reader.GetStream(0);

        }

        //
        private static void AdoNetDateTimeSample() {
            if (File.Exists("file.db")) {
                File.Delete("file.db");
            }

            using var duckDBConnection = new DuckDBConnection("Data Source=:memory:");
            duckDBConnection.Open();

            using var cmd = duckDBConnection.CreateCommand();
            cmd.CommandText = "SELECT DATE '1992-09-20';";
            using var reader = cmd.ExecuteReader();

            reader.Read();

            var type = reader.GetFieldType(0);
            var value = reader.GetValue(0);
            var value1 = reader.GetFieldValue<byte[]>(0);
            var value2 = reader.GetStream(0);

        }

        private static void AdoNetDuckDBBulkSamples() {
            if (File.Exists("file.db")) {
                File.Delete("file.db");
            }

            using System.Data.SqlClient.SqlConnection sqlConnection = new("Server=telemaco;Database=AzureDevOps_EDM Platform;Trusted_Connection=True;");
            sqlConnection.Open();

            using var sqlCommand = sqlConnection.CreateCommand();
            sqlCommand.CommandText = "SELECT * FROM  [AnalyticsModel].[tbl_WorkItem]";
            sqlCommand.CommandType = System.Data.CommandType.Text;

            using var sqlreader = sqlCommand.ExecuteReader();
            
            using var duckDBConnection = new DuckDBConnection("Data Source=:memory:");
            duckDBConnection.Open();

            using var cmdcreationtable = duckDBConnection.CreateCommand();
            cmdcreationtable.CommandText = @"CREATE TABLE entries (
""PartitionId"" INTEGER NOT NULL,
""AnalyticsCreatedDate"" TIMESTAMP NOT NULL,
""AnalyticsUpdatedDate"" TIMESTAMP NOT NULL,
""AnalyticsBatchId"" BIGINT NOT NULL,
""WorkItemRevisionSK"" INTEGER NOT NULL,
""WorkItemId"" INTEGER NOT NULL,
""Revision"" INTEGER NOT NULL,
""Watermark"" INTEGER NULL,
""Title"" VARCHAR CHECK (LENGTH(Title) <= 256) NULL,
""WorkItemType"" VARCHAR CHECK (LENGTH(WorkItemType) <= 256) NULL,
""ChangedDate"" TIMESTAMPTZ NOT NULL,
""CreatedDate"" TIMESTAMPTZ NULL,
""State"" VARCHAR CHECK (LENGTH(State) <= 256) NULL,
""Reason"" VARCHAR CHECK (LENGTH(Reason) <= 256) NULL,
""FoundIn"" VARCHAR CHECK (LENGTH(FoundIn) <= 256) NULL,
""IntegrationBuild"" VARCHAR CHECK (LENGTH(IntegrationBuild) <= 256) NULL,
""ActivatedDate"" TIMESTAMPTZ NULL,
""Activity"" VARCHAR CHECK (LENGTH(Activity) <= 256) NULL,
""BacklogPriority"" FLOAT8 NULL,
""BusinessValue"" INTEGER NULL,
""ClosedDate"" TIMESTAMPTZ NULL,
""Discipline"" VARCHAR CHECK (LENGTH(Discipline) <= 256) NULL,
""Issue"" VARCHAR CHECK (LENGTH(Issue) <= 256) NULL,
""Priority"" INTEGER NULL,
""Rating"" VARCHAR CHECK (LENGTH(Rating) <= 256) NULL,
""ResolvedDate"" TIMESTAMPTZ NULL,
""ResolvedReason"" VARCHAR CHECK (LENGTH(ResolvedReason) <= 256) NULL,
""Risk"" VARCHAR CHECK (LENGTH(Risk) <= 256) NULL,
""Severity"" VARCHAR CHECK (LENGTH(Severity) <= 256) NULL,
""StackRank"" FLOAT8 NULL,
""TimeCriticality"" FLOAT8 NULL,
""Triage"" VARCHAR CHECK (LENGTH(Triage) <= 256) NULL,
""ValueArea"" VARCHAR CHECK (LENGTH(ValueArea) <= 256) NULL,
""DueDate"" TIMESTAMPTZ NULL,
""FinishDate"" TIMESTAMPTZ NULL,
""StartDate"" TIMESTAMPTZ NULL,
""TargetDate"" TIMESTAMPTZ NULL,
""Blocked"" VARCHAR CHECK (LENGTH(Blocked) <= 256) NULL,
""Committed"" VARCHAR CHECK (LENGTH(Committed) <= 256) NULL,
""Escalate"" VARCHAR CHECK (LENGTH(Escalate) <= 256) NULL,
""FoundInEnvironment"" VARCHAR CHECK (LENGTH(FoundInEnvironment) <= 256) NULL,
""HowFound"" VARCHAR CHECK (LENGTH(HowFound) <= 256) NULL,
""Probability"" INTEGER NULL,
""RequirementType"" VARCHAR CHECK (LENGTH(RequirementType) <= 256) NULL,
""RequiresReview"" VARCHAR CHECK (LENGTH(RequiresReview) <= 256) NULL,
""RequiresTest"" VARCHAR CHECK (LENGTH(RequiresTest) <= 256) NULL,
""RootCause"" VARCHAR CHECK (LENGTH(RootCause) <= 256) NULL,
""SubjectMatterExpert1"" VARCHAR CHECK (LENGTH(SubjectMatterExpert1) <= 256) NULL,
""SubjectMatterExpert2"" VARCHAR CHECK (LENGTH(SubjectMatterExpert2) <= 256) NULL,
""SubjectMatterExpert3"" VARCHAR CHECK (LENGTH(SubjectMatterExpert3) <= 256) NULL,
""TargetResolveDate"" TIMESTAMPTZ NULL,
""TaskType"" VARCHAR CHECK (LENGTH(TaskType) <= 256) NULL,
""UserAcceptanceTest"" VARCHAR CHECK (LENGTH(UserAcceptanceTest) <= 256) NULL,
""ProjectSK"" UUID NULL,
""IsDeleted"" BOOLEAN NOT NULL,
""AutomatedTestId"" VARCHAR CHECK (LENGTH(AutomatedTestId) <= 256) NULL,
""AutomatedTestName"" VARCHAR CHECK (LENGTH(AutomatedTestName) <= 256) NULL,
""AutomatedTestStorage"" VARCHAR CHECK (LENGTH(AutomatedTestStorage) <= 256) NULL,
""AutomatedTestType"" VARCHAR CHECK (LENGTH(AutomatedTestType) <= 256) NULL,
""AutomationStatus"" VARCHAR CHECK (LENGTH(AutomationStatus) <= 256) NULL,
""DateSK"" INTEGER NULL,
""AreaSK"" UUID NULL,
""IterationSK"" UUID NULL,
""CompletedWork"" FLOAT8 NULL,
""Effort"" FLOAT8 NULL,
""OriginalEstimate"" FLOAT8 NULL,
""RemainingWork"" FLOAT8 NULL,
""Size"" FLOAT8 NULL,
""StoryPoints"" FLOAT8 NULL,
""CreatedDateSK"" INTEGER NULL,
""ActivatedDateSK"" INTEGER NULL,
""ClosedDateSK"" INTEGER NULL,
""ResolvedDateSK"" INTEGER NULL,
""AssignedToUserSK"" UUID NULL,
""ChangedByUserSK"" UUID NULL,
""CreatedByUserSK"" UUID NULL,
""ActivatedByUserSK"" UUID NULL,
""ClosedByUserSK"" UUID NULL,
""ResolvedByUserSK"" UUID NULL,
""ParentWorkItemId"" INTEGER NULL,
""TagNames"" VARCHAR CHECK (LENGTH(TagNames) <= 1024) NULL,
""StateCategory"" VARCHAR CHECK (LENGTH(StateCategory) <= 256) NULL,
""InProgressDate"" TIMESTAMPTZ NULL,
""InProgressDateSK"" INTEGER NULL,
""CompletedDate"" TIMESTAMPTZ NULL,
""CompletedDateSK"" INTEGER NULL,
""LeadTimeDays"" FLOAT8 NULL,
""CycleTimeDays"" FLOAT8 NULL,
""InternalForSnapshotHashJoin"" BOOLEAN NOT NULL,
""AuthorizedDate"" TIMESTAMPTZ NULL,
""StateChangeDate"" TIMESTAMPTZ NULL,
""StateChangeDateSK"" INTEGER NULL,
""TeamFieldSK"" INTEGER NULL,
""CommentCount"" INTEGER NULL)";
            cmdcreationtable.ExecuteNonQuery();

            var bulk = duckDBConnection.CreateBulkCopy();

            try {
                bulk.DestinationTableName = "\"main\".\"entries\"";

                bulk.NotifyAfter = 100;
                bulk.DuckDBRowsCopied += Bulk_DuckDBRowsCopied;

                var s = Stopwatch.StartNew();

                bulk.WriteToServer(sqlreader);

                s.Stop();

                Debug.WriteLine("end in {0}", s.Elapsed);

            } catch (DuckDBException e) {
                Console.WriteLine(e.Message);
            }
        }

//        private static void AdoNetDuckDBBulkSamples1() {
//            if (File.Exists("file.db")) {
//                File.Delete("file.db");
//            }

//            using var duckDBConnection = new DuckDBConnection("Data Source=:memory:");
//            duckDBConnection.Open();

//            using var cmdcreationtable = duckDBConnection.CreateCommand();
//            cmdcreationtable.CommandText = @"CREATE TABLE entries (
//""PartitionId"" INTEGER NOT NULL,
//""AnalyticsCreatedDate"" TIMESTAMP NOT NULL,
//""AnalyticsUpdatedDate"" TIMESTAMP NOT NULL,
//""AnalyticsBatchId"" BIGINT NOT NULL,
//""WorkItemRevisionSK"" INTEGER NOT NULL,
//""WorkItemId"" INTEGER NOT NULL,
//""Revision"" INTEGER NOT NULL,
//""Watermark"" INTEGER NULL,
//""Title"" VARCHAR CHECK (LENGTH(Title) <= 256) NULL,
//""WorkItemType"" VARCHAR CHECK (LENGTH(WorkItemType) <= 256) NULL,
//""ChangedDate"" TIMESTAMPTZ NOT NULL,
//""CreatedDate"" TIMESTAMPTZ NULL,
//""State"" VARCHAR CHECK (LENGTH(State) <= 256) NULL,
//""Reason"" VARCHAR CHECK (LENGTH(Reason) <= 256) NULL,
//""FoundIn"" VARCHAR CHECK (LENGTH(FoundIn) <= 256) NULL,
//""IntegrationBuild"" VARCHAR CHECK (LENGTH(IntegrationBuild) <= 256) NULL,
//""ActivatedDate"" TIMESTAMPTZ NULL,
//""Activity"" VARCHAR CHECK (LENGTH(Activity) <= 256) NULL,
//""BacklogPriority"" FLOAT8 NULL,
//""BusinessValue"" INTEGER NULL,
//""ClosedDate"" TIMESTAMPTZ NULL,
//""Discipline"" VARCHAR CHECK (LENGTH(Discipline) <= 256) NULL,
//""Issue"" VARCHAR CHECK (LENGTH(Issue) <= 256) NULL,
//""Priority"" INTEGER NULL,
//""Rating"" VARCHAR CHECK (LENGTH(Rating) <= 256) NULL,
//""ResolvedDate"" TIMESTAMPTZ NULL,
//""ResolvedReason"" VARCHAR CHECK (LENGTH(ResolvedReason) <= 256) NULL,
//""Risk"" VARCHAR CHECK (LENGTH(Risk) <= 256) NULL,
//""Severity"" VARCHAR CHECK (LENGTH(Severity) <= 256) NULL,
//""StackRank"" FLOAT8 NULL,
//""TimeCriticality"" FLOAT8 NULL,
//""Triage"" VARCHAR CHECK (LENGTH(Triage) <= 256) NULL,
//""ValueArea"" VARCHAR CHECK (LENGTH(ValueArea) <= 256) NULL,
//""DueDate"" TIMESTAMPTZ NULL,
//""FinishDate"" TIMESTAMPTZ NULL,
//""StartDate"" TIMESTAMPTZ NULL,
//""TargetDate"" TIMESTAMPTZ NULL,
//""Blocked"" VARCHAR CHECK (LENGTH(Blocked) <= 256) NULL,
//""Committed"" VARCHAR CHECK (LENGTH(Committed) <= 256) NULL,
//""Escalate"" VARCHAR CHECK (LENGTH(Escalate) <= 256) NULL,
//""FoundInEnvironment"" VARCHAR CHECK (LENGTH(FoundInEnvironment) <= 256) NULL,
//""HowFound"" VARCHAR CHECK (LENGTH(HowFound) <= 256) NULL,
//""Probability"" INTEGER NULL,
//""RequirementType"" VARCHAR CHECK (LENGTH(RequirementType) <= 256) NULL,
//""RequiresReview"" VARCHAR CHECK (LENGTH(RequiresReview) <= 256) NULL,
//""RequiresTest"" VARCHAR CHECK (LENGTH(RequiresTest) <= 256) NULL,
//""RootCause"" VARCHAR CHECK (LENGTH(RootCause) <= 256) NULL,
//""SubjectMatterExpert1"" VARCHAR CHECK (LENGTH(SubjectMatterExpert1) <= 256) NULL,
//""SubjectMatterExpert2"" VARCHAR CHECK (LENGTH(SubjectMatterExpert2) <= 256) NULL,
//""SubjectMatterExpert3"" VARCHAR CHECK (LENGTH(SubjectMatterExpert3) <= 256) NULL,
//""TargetResolveDate"" TIMESTAMPTZ NULL,
//""TaskType"" VARCHAR CHECK (LENGTH(TaskType) <= 256) NULL,
//""UserAcceptanceTest"" VARCHAR CHECK (LENGTH(UserAcceptanceTest) <= 256) NULL,
//""ProjectSK"" UUID NULL,
//""IsDeleted"" BOOLEAN NOT NULL,
//""AutomatedTestId"" VARCHAR CHECK (LENGTH(AutomatedTestId) <= 256) NULL,
//""AutomatedTestName"" VARCHAR CHECK (LENGTH(AutomatedTestName) <= 256) NULL,
//""AutomatedTestStorage"" VARCHAR CHECK (LENGTH(AutomatedTestStorage) <= 256) NULL,
//""AutomatedTestType"" VARCHAR CHECK (LENGTH(AutomatedTestType) <= 256) NULL,
//""AutomationStatus"" VARCHAR CHECK (LENGTH(AutomationStatus) <= 256) NULL,
//""DateSK"" INTEGER NULL,
//""AreaSK"" UUID NULL,
//""IterationSK"" UUID NULL,
//""CompletedWork"" FLOAT8 NULL,
//""Effort"" FLOAT8 NULL,
//""OriginalEstimate"" FLOAT8 NULL,
//""RemainingWork"" FLOAT8 NULL,
//""Size"" FLOAT8 NULL,
//""StoryPoints"" FLOAT8 NULL,
//""CreatedDateSK"" INTEGER NULL,
//""ActivatedDateSK"" INTEGER NULL,
//""ClosedDateSK"" INTEGER NULL,
//""ResolvedDateSK"" INTEGER NULL,
//""AssignedToUserSK"" UUID NULL,
//""ChangedByUserSK"" UUID NULL,
//""CreatedByUserSK"" UUID NULL,
//""ActivatedByUserSK"" UUID NULL,
//""ClosedByUserSK"" UUID NULL,
//""ResolvedByUserSK"" UUID NULL,
//""ParentWorkItemId"" INTEGER NULL,
//""TagNames"" VARCHAR CHECK (LENGTH(TagNames) <= 1024) NULL,
//""StateCategory"" VARCHAR CHECK (LENGTH(StateCategory) <= 256) NULL,
//""InProgressDate"" TIMESTAMPTZ NULL,
//""InProgressDateSK"" INTEGER NULL,
//""CompletedDate"" TIMESTAMPTZ NULL,
//""CompletedDateSK"" INTEGER NULL,
//""LeadTimeDays"" FLOAT8 NULL,
//""CycleTimeDays"" FLOAT8 NULL,
//""InternalForSnapshotHashJoin"" BOOLEAN NOT NULL,
//""AuthorizedDate"" TIMESTAMPTZ NULL,
//""StateChangeDate"" TIMESTAMPTZ NULL,
//""StateChangeDateSK"" INTEGER NULL,
//""TeamFieldSK"" INTEGER NULL,
//""CommentCount"" INTEGER NULL)";
//            cmdcreationtable.ExecuteNonQuery();

//            var bulk = duckDBConnection.CreateBulkCopy();

//            try {
//                bulk.DestinationTableName = "\"temp\".\"entries\"";

//                //bulk.NotifyAfter = 100;
//                //bulk.DuckDBRowsCopied += Bulk_DuckDBRowsCopied;

//                var s = Stopwatch.StartNew();

//                bulk.WriteToServer(sqlreader);

//                s.Stop();

//                Debug.WriteLine("end in {0}", s.Elapsed);

//            } catch (DuckDBException e) {
//                Console.WriteLine(e.Message);
//            }
//        }

        private static void AdoNetSqlBulkSamples() {
            if (File.Exists("file.db")) {
                File.Delete("file.db");
            }

            // Source
            using System.Data.SqlClient.SqlConnection sqlConnection = new("Server=telemaco;Database=AzureDevOps_EDM Platform;Trusted_Connection=True;");
            sqlConnection.Open();

            using var sqlCommand = sqlConnection.CreateCommand();
            sqlCommand.CommandText = "SELECT * FROM  [AnalyticsModel].[tbl_WorkItem]";
            sqlCommand.CommandType = System.Data.CommandType.Text;

            using var sqlreader = sqlCommand.ExecuteReader();

            // destination
            using System.Data.SqlClient.SqlConnection destination = new("Server=nb097;Database=MyTest;Trusted_Connection=True;");
            destination.Open();

            using var cmdcreationtable = destination.CreateCommand();
            cmdcreationtable.CommandText = @"CREATE TABLE entries(
	[PartitionId] [int] NOT NULL,
	[AnalyticsCreatedDate] [datetime] NOT NULL,
	[AnalyticsUpdatedDate] [datetime] NOT NULL,
	[AnalyticsBatchId] [bigint] NOT NULL,
	[WorkItemRevisionSK] [int] NOT NULL,
	[WorkItemId] [int] NOT NULL,
	[Revision] [int] NOT NULL,
	[Watermark] [int] NULL,
	[Title] [nvarchar](256) NULL,
	[WorkItemType] [nvarchar](256) NULL,
	[ChangedDate] [datetimeoffset](7) NOT NULL,
	[CreatedDate] [datetimeoffset](7) NULL,
	[State] [nvarchar](256) NULL,
	[Reason] [nvarchar](256) NULL,
	[FoundIn] [nvarchar](256) NULL,
	[IntegrationBuild] [nvarchar](256) NULL,
	[ActivatedDate] [datetimeoffset](7) NULL,
	[Activity] [nvarchar](256) NULL,
	[BacklogPriority] [float] NULL,
	[BusinessValue] [int] NULL,
	[ClosedDate] [datetimeoffset](7) NULL,
	[Discipline] [nvarchar](256) NULL,
	[Issue] [nvarchar](256) NULL,
	[Priority] [int] NULL,
	[Rating] [nvarchar](256) NULL,
	[ResolvedDate] [datetimeoffset](7) NULL,
	[ResolvedReason] [nvarchar](256) NULL,
	[Risk] [nvarchar](256) NULL,
	[Severity] [nvarchar](256) NULL,
	[StackRank] [float] NULL,
	[TimeCriticality] [float] NULL,
	[Triage] [nvarchar](256) NULL,
	[ValueArea] [nvarchar](256) NULL,
	[DueDate] [datetimeoffset](7) NULL,
	[FinishDate] [datetimeoffset](7) NULL,
	[StartDate] [datetimeoffset](7) NULL,
	[TargetDate] [datetimeoffset](7) NULL,
	[Blocked] [nvarchar](256) NULL,
	[Committed] [nvarchar](256) NULL,
	[Escalate] [nvarchar](256) NULL,
	[FoundInEnvironment] [nvarchar](256) NULL,
	[HowFound] [nvarchar](256) NULL,
	[Probability] [int] NULL,
	[RequirementType] [nvarchar](256) NULL,
	[RequiresReview] [nvarchar](256) NULL,
	[RequiresTest] [nvarchar](256) NULL,
	[RootCause] [nvarchar](256) NULL,
	[SubjectMatterExpert1] [nvarchar](256) NULL,
	[SubjectMatterExpert2] [nvarchar](256) NULL,
	[SubjectMatterExpert3] [nvarchar](256) NULL,
	[TargetResolveDate] [datetimeoffset](7) NULL,
	[TaskType] [nvarchar](256) NULL,
	[UserAcceptanceTest] [nvarchar](256) NULL,
	[ProjectSK] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[AutomatedTestId] [nvarchar](256) NULL,
	[AutomatedTestName] [nvarchar](256) NULL,
	[AutomatedTestStorage] [nvarchar](256) NULL,
	[AutomatedTestType] [nvarchar](256) NULL,
	[AutomationStatus] [nvarchar](256) NULL,
	[DateSK] [int] NULL,
	[AreaSK] [uniqueidentifier] NULL,
	[IterationSK] [uniqueidentifier] NULL,
	[CompletedWork] [float] NULL,
	[Effort] [float] NULL,
	[OriginalEstimate] [float] NULL,
	[RemainingWork] [float] NULL,
	[Size] [float] NULL,
	[StoryPoints] [float] NULL,
	[CreatedDateSK] [int] NULL,
	[ActivatedDateSK] [int] NULL,
	[ClosedDateSK] [int] NULL,
	[ResolvedDateSK] [int] NULL,
	[AssignedToUserSK] [uniqueidentifier] NULL,
	[ChangedByUserSK] [uniqueidentifier] NULL,
	[CreatedByUserSK] [uniqueidentifier] NULL,
	[ActivatedByUserSK] [uniqueidentifier] NULL,
	[ClosedByUserSK] [uniqueidentifier] NULL,
	[ResolvedByUserSK] [uniqueidentifier] NULL,
	[ParentWorkItemId] [int] NULL,
	[TagNames] [nvarchar](1024) NULL,
	[StateCategory] [nvarchar](256) NULL,
	[InProgressDate] [datetimeoffset](7) NULL,
	[InProgressDateSK] [int] NULL,
	[CompletedDate] [datetimeoffset](7) NULL,
	[CompletedDateSK] [int] NULL,
	[LeadTimeDays] [float] NULL,
	[CycleTimeDays] [float] NULL,
	[InternalForSnapshotHashJoin] [bit] NOT NULL,
	[AuthorizedDate] [datetimeoffset](7) NULL,
	[StateChangeDate] [datetimeoffset](7) NULL,
	[StateChangeDateSK] [int] NULL,
	[TeamFieldSK] [int] NULL,
	[CommentCount] [int] NULL
) ";
            cmdcreationtable.ExecuteNonQuery();

            using var bulk = new SqlBulkCopy(destination, SqlBulkCopyOptions.UseInternalTransaction, destination.BeginTransaction());

            try {
                bulk.DestinationTableName = "entries";
                bulk.NotifyAfter = 10;
                //bulk.DuckDBRowsCopied += Bulk_DuckDBRowsCopied;

                var s = Stopwatch.StartNew();

                bulk.WriteToServer(sqlreader);

                s.Stop();

                Debug.WriteLine("end in {0}", s.Elapsed);

            } catch (DuckDBException e) {
                Console.WriteLine(e.Message);
            }
        }

        private static void Bulk_DuckDBRowsCopied(object sender, DuckDBRowsCopiedEventArgs e) {
            Debug.WriteLine(e.RowsCopied);
        }

        private static void LowLevelBindingsSample()
        {
            var result = Startup.DuckDBOpen(new SafeUnmanagedMemoryHandle(IntPtr.Zero), out var database);

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
                    Console.Write(val);
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
                var columnName = Query.DuckDBColumnName(ref queryResult, index).ToManagedString(false);
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
