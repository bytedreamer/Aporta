using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public class DoorRepository : JsonDocumentRepository<Door>
{
    public DoorRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "door";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM door";

    protected override void SetId(Door entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Door entity)
    {
        return entity.Id;
    }
}
