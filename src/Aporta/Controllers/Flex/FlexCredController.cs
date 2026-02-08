using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("flex/cred")]
public class FlexCredController : FlexCrudControllerBase<Cred, FlexCred>
{
    private readonly Z9CredRepository _repository;

    public FlexCredController(IDataAccess dataAccess)
    {
        _repository = new Z9CredRepository(dataAccess);
    }

    protected override ProtoJsonRepository<Cred> Repository => _repository;
    protected override FlexCred ToFlex(Cred proto) => FlexMapper.ToFlex(proto);
    protected override Cred ToProto(FlexCred flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexCred flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(Cred proto) => proto.Uuid;
}
