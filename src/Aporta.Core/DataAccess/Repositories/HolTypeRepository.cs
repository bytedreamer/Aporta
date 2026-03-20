using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class HolTypeRepository : ProtoJsonRepository<HolType>
{
    private readonly IDataAccess _dataAccess;

    public HolTypeRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "hol_type";
    protected override int GetUnid(HolType message) => message.Unid;
}
