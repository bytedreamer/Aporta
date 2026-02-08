using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class EncryptionKeyRepository : ProtoJsonRepository<EncryptionKey>
{
    private readonly IDataAccess _dataAccess;

    public EncryptionKeyRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "encryption_key";
    protected override int GetUnid(EncryptionKey message) => message.Unid;
}
