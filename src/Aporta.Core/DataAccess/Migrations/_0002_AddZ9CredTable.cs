using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations;

public class _0002_AddZ9CredTable : IMigration
{
    public int Version => 102;

    public string Name => "Add Z9 Cred table for storing full credential proto with privilege bindings";

    public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            @"
            -- Z9 Credential table (stores full proto including privBindings)
            CREATE TABLE z9_cred (
                id INTEGER NOT NULL PRIMARY KEY,
                data TEXT NOT NULL
            );
            ",
            transaction: transaction);
    }
}
