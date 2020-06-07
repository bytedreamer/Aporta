using System;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0001_AddExtensionTable : IMigration
    {
        public int Version => 1;
        
        public string Name => "Add extension table";

        public async Task PerformUpdate(IDataAccess dataAccess)
        {
            using var connection = dataAccess.CreateDbConnection();
            connection.Open();
            var transaction = connection.BeginTransaction();
            await connection.ExecuteAsync(
                @"create table extension
                        (
                            id text not null
                                constraint extension_pk
                                    primary key,
                            name text not null,
                            enabled integer default 0 not null
                        );

                        create unique index extension_id_uindex
                            on extension (id);",
                transaction: transaction);

            await connection.ExecuteAsync(
                @"insert into schema_info (id, name, timestamp)
                        values (@id, @name, @timestamp)",
                new {id = Version, name = Name, timestamp = DateTime.UtcNow}, transaction);
            transaction.Commit();
        }
    }
}