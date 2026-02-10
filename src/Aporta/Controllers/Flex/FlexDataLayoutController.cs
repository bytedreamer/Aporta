using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("dataLayout")]
public class FlexDataLayoutController : FlexCrudControllerBase<DataLayout, FlexDataLayout>
{
    private readonly DataLayoutRepository _repository;

    public FlexDataLayoutController(IDataAccess dataAccess)
    {
        _repository = new DataLayoutRepository(dataAccess);
    }

    protected override ProtoJsonRepository<DataLayout> Repository => _repository;
    protected override FlexDataLayout ToFlex(DataLayout proto) => FlexMapper.ToFlex(proto);
    protected override DataLayout ToProto(FlexDataLayout flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDataLayout flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(DataLayout proto) => proto.Uuid;
}
