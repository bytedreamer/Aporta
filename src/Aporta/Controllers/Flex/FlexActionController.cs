using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
public class FlexActionController : ControllerBase
{
    private readonly Z9OpenCommunityProtocolService _protocolService;

    public FlexActionController(Z9OpenCommunityProtocolService protocolService)
    {
        _protocolService = protocolService;
    }

    [HttpGet("flex/json/doorModeChange")]
    public IActionResult DoorModeChange([FromQuery] int? unid, [FromQuery] string uuid, [FromQuery] string tag, [FromQuery] string value)
    {
        var doorUnid = unid ?? 0;

        var req = new DevActionReq
        {
            RequestId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DevActionType = DevActionType.DoorModeChange,
            DevUnid = doorUnid,
        };

        if (string.Equals(value, "RESET", System.StringComparison.OrdinalIgnoreCase))
        {
            req.DevActionParams = new DevActionParams
            {
                Type = DevActionParamsType.DoorMode,
                ExtDoorModeDevActionParams = new DoorModeDevActionParams { ResetToDefault = true },
            };
        }
        else if (int.TryParse(value, out var modeInt))
        {
            var doorMode = CommonDoorModes.ForDoorModeType((DoorModeType)modeInt);
            req.DevActionParams = new DevActionParams
            {
                Type = DevActionParamsType.DoorMode,
                ExtDoorModeDevActionParams = new DoorModeDevActionParams { DoorMode = doorMode },
            };
        }

        _protocolService.DispatchDevAction(req);
        return Ok(new FlexVoid());
    }

    [HttpGet("flex/json/doorMomentaryUnlock")]
    public IActionResult DoorMomentaryUnlock([FromQuery] int? unid, [FromQuery] string uuid, [FromQuery] string tag, [FromQuery] bool? extDoorTime)
    {
        var doorUnid = unid ?? 0;

        var req = new DevActionReq
        {
            RequestId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DevActionType = DevActionType.DoorMomentaryUnlock,
            DevUnid = doorUnid,
        };

        _protocolService.DispatchDevAction(req);
        return Ok(new FlexVoid());
    }
}
