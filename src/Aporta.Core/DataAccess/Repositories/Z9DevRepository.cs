using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class Z9DevRepository : ProtoJsonRepository<Dev>
{
    private readonly IDataAccess _dataAccess;

    public Z9DevRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "z9_dev";
    protected override int GetUnid(Dev message) => message.Unid;
}
