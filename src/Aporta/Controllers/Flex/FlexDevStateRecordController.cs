using System.Linq;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("flex/devStateRecord")]
public class FlexDevStateRecordController : ControllerBase
{
    private readonly DevStateService _devStateService;

    public FlexDevStateRecordController(DevStateService devStateService)
    {
        _devStateService = devStateService;
    }

    [HttpGet("list")]
    public IActionResult List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = _devStateService.GetAllDevStateRecords();
        var paged = all.Skip(offset).Take(max).Select(FlexMapper.ToFlex).ToList();

        return Ok(new FlexListResponse<FlexDevStateRecord>
        {
            Offset = offset,
            Max = max,
            Count = all.Count,
            InstanceList = paged,
        });
    }
}
