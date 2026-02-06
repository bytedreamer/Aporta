using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

/// <summary>
/// Repository for storing the full Z9 Cred proto messages (including privBindings).
/// This is separate from CredentialRepository which stores Aporta's legacy credential format.
/// </summary>
public class Z9CredRepository : ProtoJsonRepository<Cred>
{
    private readonly IDataAccess _dataAccess;

    public Z9CredRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "z9_cred";
    protected override int GetUnid(Cred message) => message.Unid;
}
