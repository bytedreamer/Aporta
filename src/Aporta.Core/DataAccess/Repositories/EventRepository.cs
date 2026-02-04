using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Shared.Models;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories;

public class EventRepository : JsonDocumentRepository<Event>
{
    public EventRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "event";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM event";

    public async Task<int> Insert(Event record)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var json = JsonSerializer.Serialize(record, GetJsonOptions());
        int id = await connection.QueryFirstAsync<int>(
            "INSERT INTO event (data) VALUES (@data); SELECT last_insert_rowid()",
            new { data = json });

        SetId(record, id);
        return id;
    }

    protected override void SetId(Event entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Event entity)
    {
        return entity.Id;
    }
}
