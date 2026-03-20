using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class DataFormatRepository : ProtoJsonRepository<DataFormat>
{
    private readonly IDataAccess _dataAccess;

    public DataFormatRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "data_format";
    protected override int GetUnid(DataFormat message) => message.Unid;
}
