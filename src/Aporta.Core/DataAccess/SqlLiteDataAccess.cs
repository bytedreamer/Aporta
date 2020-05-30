using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Aporta.Core.DataAccess.Migrations;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Aporta.Core.DataAccess
{
    public class SqlLiteDataAccess : IDataAccess
    {
        private const string MemorySource = "Aporta;Mode=Memory;Cache=Shared";
        private const string FileName = "Aporta.sqlite";
        private readonly bool _inMemory;

       /// <summary>
       /// 
       /// </summary>
       /// <param name="inMemory">Set to true if database is temporarily created in memory</param>
        public SqlLiteDataAccess(bool inMemory = false)
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
            return Path.Combine(Environment.CurrentDirectory, FileName);
        }

        public async Task<int> CurrentVersion()
        {
            using var connection = CreateDbConnection();
            connection.Open();
            return await connection.QueryFirstAsync<int>(
                @"SELECT id
                        FROM schema_info
                        ORDER BY id DESC");
        }

        public async Task UpdateSchema()
        {
            int version = -1;
            if (!_inMemory && File.Exists(BuildFilePath()))
            {
                version = await CurrentVersion();
            }

            if (version < 0)
            {
                var migration = new _0001_InitialCreate();
                await migration.PerformUpdate(this);
            }
        }
    }
}