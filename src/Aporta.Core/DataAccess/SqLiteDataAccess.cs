using System;
using System.Data;
using System.IO;
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
        new _0000_InitialCreate(),
        new _0001_AddExtensionTable(),
        new _0002_AddEndpointTable(),
        new _0003_AddOutputTable(),
        new _0004_AddInputTable(),
        new _0005_AddDoorTable(),
        new _0006_AddGlobalSettingTable(),
        new _0007_AddCredentialTable(), 
        new _0008_AddPersonTable(),
        new _0009_AddEventTable(),
        new _0010_AddLastEventToCredentialTable()
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

        using var connection = CreateDbConnection();

        connection.Open();
        using var transaction = connection.BeginTransaction();

        for (int migrationIndex = currentVersion + 1; migrationIndex < _migrations.Length; migrationIndex++)
        {
            await _migrations[migrationIndex].PerformUpdate(connection, transaction);

            await connection.ExecuteAsync(
                @"insert into schema_info (id, name, timestamp)
                        values (@id, @name, @timestamp)",
                new
                {
                    id = _migrations[migrationIndex].Version, name = _migrations[migrationIndex].Name,
                    timestamp = DateTime.UtcNow
                }, transaction);
        }

        transaction.Commit();
    }
}