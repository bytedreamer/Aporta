using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Aporta.Core.DataAccess.Migrations;

public class _0003_AddZ9EvtTable : IMigration
{
    public int Version => 103;

    public string Name => "Add Z9 Evt table for storing event proto messages";

    public async Task PerformUpdate(IDbConnection connection, IDbTransaction transaction)
    {
        await connection.ExecuteAsync(
            @"
            CREATE TABLE z9_evt (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                data TEXT NOT NULL
            );
            ",
            transaction: transaction);
    }
}
