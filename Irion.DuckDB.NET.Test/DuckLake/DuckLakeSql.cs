using Irion.DuckDB.NET.Test.S3;

namespace Irion.DuckDB.NET.Test.DuckLake;

public static class DuckLakeSql
{
    public static string CreatePostgresMetadataSecret(
        string secretName,
        DuckLakePostgresConnectionOptions postgres)
    {
        return
            $"""
            CREATE OR REPLACE SECRET {secretName} (
                TYPE postgres,
                HOST {SqlLiterals.DuckDbString(postgres.Host)},
                PORT {postgres.Port},
                DATABASE {SqlLiterals.DuckDbString(postgres.Database)},
                USER {SqlLiterals.DuckDbString(postgres.User)},
                PASSWORD {SqlLiterals.DuckDbString(postgres.Password)}
            );
            """;
    }

    public static string CreateS3Secret(
        string secretName,
        MinioContainer minio,
        string scope)
    {
        return
            $"""
            CREATE OR REPLACE SECRET {secretName} (
                TYPE s3,
                PROVIDER config,
                KEY_ID {SqlLiterals.DuckDbString(minio.GetAccessKey())},
                SECRET {SqlLiterals.DuckDbString(minio.GetSecretKey())},
                REGION 'us-east-1',
                ENDPOINT {SqlLiterals.DuckDbString(minio.GetDuckDbS3Endpoint())},
                URL_STYLE 'path',
                USE_SSL false,
                SCOPE {SqlLiterals.DuckDbString(scope)}
            );
            """;
    }

    public static string CreateDuckLakeSecret(
        string secretName,
        string postgresSecretName,
        string dataPath)
    {
        return
            $$"""
            CREATE OR REPLACE SECRET {{secretName}} (
                TYPE ducklake,
                METADATA_PATH '',
                METADATA_SCHEMA 'public',
                DATA_PATH {{SqlLiterals.DuckDbString(dataPath)}},
                METADATA_PARAMETERS MAP {'TYPE': 'postgres', 'SECRET': '{{postgresSecretName}}'}
            );
            """;
    }

    public static string NormalizeFileDataPath(string dataPath)
    {
        return Path.GetFullPath(dataPath).Replace('\\', '/') + "/";
    }

    public static string CreateIdentifierSuffix()
    {
        return Guid.NewGuid().ToString("N");
    }
}
