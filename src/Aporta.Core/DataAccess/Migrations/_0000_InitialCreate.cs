using System;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0000_InitialCreate : IMigration
    {
        public int Version => 0;
        
        public string Name => "Initial create";

        public async Task PerformUpdate(IDataAccess dataAccess)
        {
            using var connection = dataAccess.CreateDbConnection();
            connection.Open();
            var transaction = connection.BeginTransaction();
            await connection.ExecuteAsync(
                @"create table schema_info
                        (
                            id        integer
                                constraint schema_info_pk
                                    primary key,
                            name      text not null,
                            timestamp datetime
                        );

                        create unique index schema_info_id_uindex
                            on schema_info (id);", 
                transaction: transaction);

            await connection.ExecuteAsync(
                @"insert into schema_info (id, name, timestamp)
                        values (@id, @name, @timestamp)", 
                new {id = Version, name = Name, timestamp = DateTime.UtcNow}, transaction);
            transaction.Commit();
        }
    }
}