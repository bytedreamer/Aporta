using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0002_AddEndpointTable : IMigration
    {
        public int Version => 2;
        
        public string Name => "Add endpoint table";

        public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
        {
            await connection.ExecuteAsync(
                @"create table endpoint
                    (
                        id            integer not null
                            constraint endpoint_pk
                                primary key autoincrement,
                        name          text    not null,
                        configuration text    not null,
                        endpoint_type integer not null,
                        extension_id  text    not null
                            references extension
                                on update cascade on delete cascade
                    );

                    create unique index endpoint_id_uindex
                        on endpoint (id);",
                transaction: transaction);
        }
    }
}