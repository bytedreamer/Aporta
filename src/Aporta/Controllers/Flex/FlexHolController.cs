using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("hol")]
public class FlexHolController : FlexCrudControllerBase<Hol, FlexHol>
{
    private readonly HolRepository _repository;

    public FlexHolController(IDataAccess dataAccess)
    {
        _repository = new HolRepository(dataAccess);
    }

    protected override ProtoJsonRepository<Hol> Repository => _repository;
    protected override FlexHol ToFlex(Hol proto) => FlexMapper.ToFlex(proto);
    protected override Hol ToProto(FlexHol flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexHol flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexHol flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(Hol proto) => proto.Uuid;
}
