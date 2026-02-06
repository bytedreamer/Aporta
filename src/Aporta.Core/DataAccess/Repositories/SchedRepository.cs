using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class SchedRepository : ProtoJsonRepository<Sched>
{
    private readonly IDataAccess _dataAccess;

    public SchedRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "sched";
    protected override int GetUnid(Sched message) => message.Unid;
}
