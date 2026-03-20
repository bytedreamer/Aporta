using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("holCal")]
public class FlexHolCalController : FlexCrudControllerBase<HolCal, FlexHolCal>
{
    private readonly HolCalRepository _repository;

    public FlexHolCalController(IDataAccess dataAccess)
    {
        _repository = new HolCalRepository(dataAccess);
    }

    protected override ProtoJsonRepository<HolCal> Repository => _repository;
    protected override FlexHolCal ToFlex(HolCal proto) => FlexMapper.ToFlex(proto);
    protected override HolCal ToProto(FlexHolCal flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexHolCal flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexHolCal flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(HolCal proto) => proto.Uuid;
}
