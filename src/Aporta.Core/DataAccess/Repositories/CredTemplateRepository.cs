using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

public class CredTemplateRepository : ProtoJsonRepository<CredTemplate>
{
    private readonly IDataAccess _dataAccess;

    public CredTemplateRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "cred_template";
    protected override int GetUnid(CredTemplate message) => message.Unid;
}
