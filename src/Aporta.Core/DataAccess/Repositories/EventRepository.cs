using Aporta.Shared.Models;

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

    protected override void SetId(Event entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Event entity)
    {
        return entity.Id;
    }
}
