using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations;

public class _0001_AddZ9OpenTables : IMigration
{
    public int Version => 101;

    public string Name => "Add Z9 Open protobuf storage tables";

    public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            @"
            -- Credential Template table
            CREATE TABLE cred_template (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Data Layout table
            CREATE TABLE data_layout (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Data Format table
            CREATE TABLE data_format (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Privilege table
            CREATE TABLE priv (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Schedule table
            CREATE TABLE sched (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Holiday table
            CREATE TABLE hol (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Holiday Calendar table
            CREATE TABLE hol_cal (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

            -- Holiday Type table
            CREATE TABLE hol_type (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );

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
            ",
            transaction: transaction);
    }
}
