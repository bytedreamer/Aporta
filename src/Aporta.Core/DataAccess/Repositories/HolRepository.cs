using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class HolRepository : ProtoJsonRepository<Hol>
{
    private readonly IDataAccess _dataAccess;

    public HolRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "hol";
    protected override int GetUnid(Hol message) => message.Unid;
}
