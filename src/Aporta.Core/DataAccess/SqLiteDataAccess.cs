using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Aporta.Core.DataAccess.Migrations;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Aporta.Core.DataAccess;

public class SqLiteDataAccess : IDataAccess
{
    private const string MemorySource = "Aporta;Mode=Memory;Cache=Shared";
    private const string FileName = "Data/Aporta.sqlite";
    private readonly bool _inMemory;

    private readonly IMigration[] _migrations =
    {
        new _0000_InitialCreate()
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="inMemory">Set to true if database is temporarily created in memory</param>
    public SqLiteDataAccess(bool inMemory = false)
    {
        _inMemory = inMemory;
    }

    public IDbConnection CreateDbConnection()
    {
        return new SqliteConnection(BuildConnectionString());
    }

    private string BuildConnectionString()
    {
        string connectionString = "Data Source=" + (_inMemory
            ? MemorySource
            : BuildFilePath());
        return connectionString;
    }

    private static string BuildFilePath()
    {
        string path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory;
            
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
            
        return Path.Combine(path, FileName);
    }

    public async Task<int> CurrentVersion()
    {
        using var connection = CreateDbConnection();
        connection.Open();

        if (await connection.ExecuteScalarAsync<int>(
                @"select count(*)
                from sqlite_master
                where tbl_name = 'schema_info'") == 0)
        {
            return -1;
        }
            
        return await connection.QueryFirstAsync<int>(
            @"select id
                        from schema_info
                        order by id desc");
    }

    public async Task UpdateSchema()
    {
        int currentVersion;
        if (!_inMemory && !File.Exists(BuildFilePath()))
        {
            currentVersion = -1;
        }
        else
        {
            currentVersion = await CurrentVersion();
        }

        // Detect old column-based schema (versions 0-10) and require migration tool
        if (currentVersion >= 0 && currentVersion <= 10)
        {
            throw new InvalidOperationException(
                $"Database is using old schema (version {currentVersion}). " +
                "Please run the Aporta.Migration tool to upgrade to JSON document storage before starting the application. " +
                "Usage: dotnet Aporta.Migration.dll <path-to-database>");
        }

        using var connection = CreateDbConnection();

        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var migration in _migrations.Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
        {
            await migration.PerformUpdate(connection, transaction);

            await connection.ExecuteAsync(
                @"insert into schema_info (id, name, timestamp)
                        values (@id, @name, @timestamp)",
                new
                {
                    id = migration.Version, name = migration.Name,
                    timestamp = DateTime.UtcNow
                }, transaction);
        }

        transaction.Commit();
    }
}