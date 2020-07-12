using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations
{
    public class _0001_AddExtensionTable : IMigration
    {
        public int Version => 1;
        
        public string Name => "Add extension table";

        public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
        {
            await connection.ExecuteAsync(
                @"create table extension
                        (
                            id text not null
                                constraint extension_pk
                                    primary key,
                            name text not null,
                            enabled integer default 0 not null,
                            configuration text not null
                        );

                        create unique index extension_id_uindex
                            on extension (id);",
                transaction: transaction);
        }
    }
}