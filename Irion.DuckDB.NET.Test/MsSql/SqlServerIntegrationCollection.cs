namespace Irion.DuckDB.NET.Test.MsSql;

[CollectionDefinition(Name)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
    public const string Name = "MSSQL integration";
}
