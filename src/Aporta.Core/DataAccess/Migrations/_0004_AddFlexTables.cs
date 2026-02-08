using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations;

public class _0004_AddFlexTables : IMigration
{
    public int Version => 104;

    public string Name => "Add tables for Z9/Flex Community REST API";

    public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            @"
            -- Z9 Device table (stores full Dev proto messages)
            CREATE TABLE z9_dev (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Encryption Key table
            CREATE TABLE encryption_key (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Flex session table (for REST API authentication)
            CREATE TABLE flex_session (
                token TEXT NOT NULL PRIMARY KEY,
                username TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            ",
            transaction: transaction);
    }
}
