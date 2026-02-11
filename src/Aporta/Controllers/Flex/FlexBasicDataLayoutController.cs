using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("basicDataLayout")]
public class FlexBasicDataLayoutController : FlexCrudControllerBase<DataLayout, FlexDataLayout>
{
    private readonly DataLayoutRepository _repository;

    public FlexBasicDataLayoutController(IDataAccess dataAccess)
    {
        _repository = new DataLayoutRepository(dataAccess);
    }

    protected override ProtoJsonRepository<DataLayout> Repository => _repository;
    protected override FlexDataLayout ToFlex(DataLayout proto) => FlexMapper.ToFlex(proto);
    protected override DataLayout ToProto(FlexDataLayout flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDataLayout flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexDataLayout flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(DataLayout proto) => proto.Uuid;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll())
            .Where(dl => dl.LayoutType == DataLayoutType.Basic)
            .ToList();
        var count = all.Count;
        var page = all.Skip(offset).Take(max).Select(ToFlex).ToList();

        return Ok(new FlexListResponse<FlexDataLayout>
        {
            Offset = offset,
            Max = max,
            Count = count,
            InstanceList = page,
        });
    }
}
