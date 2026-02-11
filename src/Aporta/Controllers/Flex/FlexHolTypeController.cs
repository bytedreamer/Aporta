using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("holType")]
public class FlexHolTypeController : FlexCrudControllerBase<HolType, FlexHolType>
{
    private readonly HolTypeRepository _repository;

    public FlexHolTypeController(IDataAccess dataAccess)
    {
        _repository = new HolTypeRepository(dataAccess);
    }

    protected override ProtoJsonRepository<HolType> Repository => _repository;
    protected override FlexHolType ToFlex(HolType proto) => FlexMapper.ToFlex(proto);
    protected override HolType ToProto(FlexHolType flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexHolType flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexHolType flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(HolType proto) => proto.Uuid;
}
