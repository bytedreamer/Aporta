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
[Route("evt")]
public class FlexEvtController : ControllerBase
{
    private readonly Z9EvtRepository _repository;

    public FlexEvtController(IDataAccess dataAccess)
    {
        _repository = new Z9EvtRepository(dataAccess);
    }

    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        // Z9EvtRepository uses page-based pagination; convert offset/max
        var pageNumber = (offset / max) + 1;
        var page = await _repository.GetAll(pageNumber, max);

        return Ok(new FlexListResponse<FlexEvt>
        {
            Offset = offset,
            Max = max,
            Count = page.TotalItems,
            InstanceList = page.Items.Select(FlexMapper.ToFlex).ToList(),
        });
    }

    [HttpGet("raw-reads")]
    public async Task<IActionResult> RawReads()
    {
        var rawReads = await _repository.GetUnconsumedRawReads();

        return Ok(new FlexListResponse<FlexEvt>
        {
            Offset = 0,
            Max = 100,
            Count = rawReads.Count(),
            InstanceList = rawReads.Select(FlexMapper.ToFlex).ToList(),
        });
    }

    [HttpGet("show/{id}")]
    public async Task<IActionResult> Show(string id)
    {
        if (!int.TryParse(id, out var unid))
            return NotFound();

        var evt = await _repository.Get(unid);
        if (evt == null)
            return NotFound();

        return Ok(new FlexInstanceResponse<FlexEvt> { Instance = FlexMapper.ToFlex(evt) });
    }
}
