using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class DataLayoutRepository : ProtoJsonRepository<DataLayout>
{
    private readonly IDataAccess _dataAccess;

    public DataLayoutRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "data_layout";
    protected override int GetUnid(DataLayout message) => message.Unid;
}
