using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class HolCalRepository : ProtoJsonRepository<HolCal>
{
    private readonly IDataAccess _dataAccess;

    public HolCalRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "hol_cal";
    protected override int GetUnid(HolCal message) => message.Unid;
}
