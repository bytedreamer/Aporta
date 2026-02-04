using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0000_InitialCreate : IMigration
    {
        public int Version => 0;

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
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX endpoint_id_uindex ON endpoint (id);

                -- Output table
                CREATE TABLE output (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX output_id_uindex ON output (id);

                -- Input table
                CREATE TABLE input (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX input_id_uindex ON input (id);

                -- Door table
                CREATE TABLE door (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX door_id_uindex ON door (id);

                -- Global setting table (string primary key)
                CREATE TABLE global_setting (
                    name TEXT NOT NULL PRIMARY KEY,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX global_setting_name_uindex ON global_setting (name);

                -- Credential table
                CREATE TABLE credential (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX credential_id_uindex ON credential (id);
                CREATE UNIQUE INDEX credential_number_uindex ON credential (json_extract(data, '$.number'));

                -- Person table
                CREATE TABLE person (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX person_id_uindex ON person (id);

                -- Credential assignment table (junction table as document)
                CREATE TABLE credential_assignment (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE INDEX idx_ca_credential ON credential_assignment (json_extract(data, '$.credentialId'));
                CREATE INDEX idx_ca_person ON credential_assignment (json_extract(data, '$.personId'));

                -- Event table
                CREATE TABLE event (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX event_id_uindex ON event (id);
                ",
                transaction: transaction);
        }
    }
}