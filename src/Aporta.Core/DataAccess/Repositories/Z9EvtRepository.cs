using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;
using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

/// <summary>
/// Repository for storing Z9 Evt proto messages with auto-increment IDs.
/// Events are append-only, so this uses INSERT with AUTOINCREMENT rather than upsert-by-unid.
/// </summary>
public class Z9EvtRepository : ProtoJsonRepository<Evt>
{
    private readonly IDataAccess _dataAccess;

    public Z9EvtRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "z9_evt";
    protected override int GetUnid(Evt message) => (int)message.Unid;

    /// <summary>
    /// Inserts an event with auto-increment ID. Sets message.Unid to the new ID.
    /// </summary>
    public async Task<int> Insert(Evt message)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = Formatter.Format(message);
        int id = await connection.QueryFirstAsync<int>(
            "INSERT INTO z9_evt (data) VALUES (@data); SELECT last_insert_rowid()",
            new { data = json });

        message.Unid = id;
        return id;
    }

    /// <summary>
    /// Gets an event by its auto-increment ID, setting Unid from the row ID.
    /// </summary>
    public new async Task<Evt> Get(int id)
    {
        var evt = await base.Get(id);
        if (evt != null)
            evt.Unid = id;
        return evt;
    }

    /// <summary>
    /// Returns a paginated list of events ordered by id descending (most recent first).
    /// Sets Unid from the row ID on each event.
    /// </summary>
    public async Task<PaginatedItemsDto<Evt>> GetAll(int pageNumber, int pageSize)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        int offset = (pageNumber - 1) * pageSize;
        var results = await connection.QueryAsync<(int id, string data)>(
            "SELECT id, data FROM z9_evt ORDER BY id DESC LIMIT @offset, @pageSize",
            new { offset, pageSize });

        var items = results.Select(row =>
        {
            var evt = Parser.Parse<Evt>(row.data);
            evt.Unid = row.id;
            return evt;
        });

        return new PaginatedItemsDto<Evt>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM z9_evt")
        };
    }

    /// <summary>
    /// Returns unconsumed RAW_CRED_READ events, most recent first.
    /// </summary>
    public async Task<IEnumerable<Evt>> GetUnconsumedRawReads()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var results = await connection.QueryAsync<(int id, string data)>(
            @"SELECT id, data FROM z9_evt
              WHERE json_extract(data, '$.evtCode') = @evtCode
                AND (json_extract(data, '$.consumed') IS NULL
                  OR json_extract(data, '$.consumed') = false)
              ORDER BY id DESC",
            new { evtCode = "EvtCode_RAW_CRED_READ" });

        return results.Select(row =>
        {
            var evt = Parser.Parse<Evt>(row.data);
            evt.Unid = row.id;
            return evt;
        });
    }

    /// <summary>
    /// Marks an event as consumed by updating the proto JSON.
    /// </summary>
    public async Task MarkConsumed(int id)
    {
        var evt = await Get(id);
        if (evt == null) return;

        evt.Consumed = true;

        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = Formatter.Format(evt);
        await connection.ExecuteAsync(
            "UPDATE z9_evt SET data = @data WHERE id = @id",
            new { id, data = json });
    }
}
