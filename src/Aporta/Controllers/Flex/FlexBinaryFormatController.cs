using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

/// <summary>
/// BinaryFormat is a subclass of DataFormat with dataFormatType == BINARY (1).
/// This controller filters DataFormats to only those with binary format extensions.
/// </summary>
[ApiController]
[Route("flex/binaryFormat")]
public class FlexBinaryFormatController : FlexCrudControllerBase<DataFormat, FlexDataFormat>
{
    private readonly DataFormatRepository _repository;

    public FlexBinaryFormatController(IDataAccess dataAccess)
    {
        _repository = new DataFormatRepository(dataAccess);
    }

    protected override ProtoJsonRepository<DataFormat> Repository => _repository;
    protected override FlexDataFormat ToFlex(DataFormat proto) => FlexMapper.ToFlex(proto);
    protected override DataFormat ToProto(FlexDataFormat flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDataFormat flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(DataFormat proto) => proto.Uuid;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll())
            .Where(df => df.DataFormatType == DataFormatType.Binary)
            .ToList();
        var count = all.Count;
        var page = all.Skip(offset).Take(max).Select(ToFlex).ToList();

        return Ok(new FlexListResponse<FlexDataFormat>
        {
            Offset = offset,
            Max = max,
            Count = count,
            InstanceList = page,
        });
    }
}
