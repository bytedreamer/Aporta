using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0000_InitialCreate : IMigration
    {
        // Version 100 to distinguish from old column-based schema (versions 0-10)
        public int Version => 100;

        public string Name => "Initial create with JSON document storage";

        public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
        {
            await connection.ExecuteAsync(
                @"
                -- Schema info table (unchanged)
                CREATE TABLE schema_info (
                    id INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    timestamp DATETIME NOT NULL
                );
                CREATE UNIQUE INDEX schema_info_id_uindex ON schema_info (id);

                -- Extension table (GUID primary key)
                CREATE TABLE extension (
                    id TEXT NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX extension_id_uindex ON extension (id);

                -- Endpoint table
                CREATE TABLE endpoint (
                    id INTEGER NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );

                -- Global setting table (string primary key)
                CREATE TABLE global_setting (
                    name TEXT NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );

                -- Credential table
                CREATE TABLE credential (
                    id INTEGER NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX credential_number_uindex ON credential (json_extract(data, '$.number'));

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

                -- Z9 Credential table (stores full proto including privBindings)
                CREATE TABLE z9_cred (
                    id INTEGER NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );

                -- Z9 Event table
                CREATE TABLE z9_evt (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );

",
                transaction: transaction);
        }
    }
}
