using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0000_InitialCreate : IMigration
    {
        public int Version => 0;
        
        public string Name => "Initial create";

        public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
        {
            await connection.ExecuteAsync(
                @"create table schema_info
                        (
                            id        integer not null
                                constraint schema_info_pk
                                    primary key,
                            name      text not null,
                            timestamp datetime not null
                        );

                        create unique index schema_info_id_uindex
                            on schema_info (id);", 
                transaction: transaction);
        }
    }
}