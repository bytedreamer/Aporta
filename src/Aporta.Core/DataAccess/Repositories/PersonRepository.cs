using Aporta.Shared.Models;

namespace Aporta.Core.DataAccess.Repositories;

public class PersonRepository : JsonDocumentRepository<Person>
{
    public PersonRepository(IDataAccess dataAccess)
    {
        DataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess { get; }

    protected override string TableName => "person";

    protected override string SqlRowCount => "SELECT COUNT(*) FROM person";

    protected override void SetId(Person entity, int id)
    {
        entity.Id = id;
    }

    protected override int GetId(Person entity)
    {
        return entity.Id;
    }
}
