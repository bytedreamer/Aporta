using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class PrivRepository : ProtoJsonRepository<Priv>
{
    private readonly IDataAccess _dataAccess;

    public PrivRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "priv";
    protected override int GetUnid(Priv message) => message.Unid;
}
