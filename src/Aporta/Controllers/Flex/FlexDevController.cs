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
[Route("flex/dev")]
public class FlexDevController : FlexCrudControllerBase<Dev, FlexDev>
{
    private readonly Z9DevRepository _repository;

    public FlexDevController(IDataAccess dataAccess)
    {
        _repository = new Z9DevRepository(dataAccess);
    }

    protected override ProtoJsonRepository<Dev> Repository => _repository;
    protected override FlexDev ToFlex(Dev proto) => FlexMapper.ToFlex(proto);
    protected override Dev ToProto(FlexDev flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDev flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(Dev proto) => proto.Uuid;
}

/// <summary>
/// Base class for device-type-specific controllers that filter by DevType.
/// </summary>
public abstract class FlexDevTypeControllerBase : FlexCrudControllerBase<Dev, FlexDev>
{
    private readonly Z9DevRepository _repository;

    protected FlexDevTypeControllerBase(IDataAccess dataAccess)
    {
        _repository = new Z9DevRepository(dataAccess);
    }

    protected abstract DevType FilterDevType { get; }

    protected override ProtoJsonRepository<Dev> Repository => _repository;
    protected override FlexDev ToFlex(Dev proto) => FlexMapper.ToFlex(proto);
    protected override Dev ToProto(FlexDev flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDev flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(Dev proto) => proto.Uuid;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll())
            .Where(d => d.DevType == FilterDevType)
            .ToList();
        var count = all.Count;
        var page = all.Skip(offset).Take(max).Select(ToFlex).ToList();

        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = offset,
            Max = max,
            Count = count,
            InstanceList = page,
        });
    }
}

[ApiController]
[Route("flex/door")]
public class FlexDoorController : FlexDevTypeControllerBase
{
    public FlexDoorController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.Door;
}

[ApiController]
[Route("flex/credReader")]
public class FlexCredReaderController : FlexDevTypeControllerBase
{
    public FlexCredReaderController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.CredReader;
}

[ApiController]
[Route("flex/controller")]
public class FlexControllerController : FlexDevTypeControllerBase
{
    public FlexControllerController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.IoController;
}

[ApiController]
[Route("flex/sensor")]
public class FlexSensorController : FlexDevTypeControllerBase
{
    public FlexSensorController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.Sensor;
}

[ApiController]
[Route("flex/actuator")]
public class FlexActuatorController : FlexDevTypeControllerBase
{
    public FlexActuatorController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.Actuator;
}

[ApiController]
[Route("flex/nodeDev")]
public class FlexNodeDevController : FlexDevTypeControllerBase
{
    public FlexNodeDevController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.Reserved0;
}
