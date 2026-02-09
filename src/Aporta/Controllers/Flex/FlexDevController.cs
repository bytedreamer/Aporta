using System;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly Z9DevRepository _repository;
    private readonly DoorConfigurationService _doorConfigurationService;

    public FlexDoorController(IDataAccess dataAccess,
        DoorConfigurationService doorConfigurationService) : base(dataAccess)
    {
        _repository = new Z9DevRepository(dataAccess);
        _doorConfigurationService = doorConfigurationService;
    }

    protected override DevType FilterDevType => DevType.Door;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var doors = await _doorConfigurationService.GetAll();
        var all = doors.ToList();
        var count = all.Count;
        var page = all.Skip(offset).Take(max).Select(d =>
            new FlexDev { Unid = d.Id, Name = d.Name }).ToList();

        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = offset,
            Max = max,
            Count = count,
            InstanceList = page,
        });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] Aporta.Shared.Models.Door door)
    {
        await _doorConfigurationService.Insert(door);
        return Ok(new FlexVoid());
    }

    public override async Task<IActionResult> Delete(string id)
    {
        if (int.TryParse(id, out var unid))
        {
            await _doorConfigurationService.Delete(unid);
            return Ok(new FlexVoid());
        }
        return BadRequest();
    }

    [HttpGet("available/readers")]
    public async Task<IActionResult> AvailableReaders()
    {
        var available = await _repository.GetAvailableByDevType(DevType.CredReader);
        var list = available.Select(ToFlex).ToList();
        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = 0,
            Max = list.Count,
            Count = list.Count,
            InstanceList = list,
        });
    }

    [HttpGet("available/endpoints")]
    public async Task<IActionResult> AvailableEndpoints()
    {
        var endpoints = await _doorConfigurationService.AvailableEndPoints();
        var list = endpoints.Select(e => new FlexDev
        {
            Unid = e.Id,
            Name = e.Name,
            ExternalId = e.DriverEndpointId,
        }).ToList();
        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = 0,
            Max = list.Count,
            Count = list.Count,
            InstanceList = list,
        });
    }
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
    private readonly Z9DevRepository _repository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;

    public FlexSensorController(IDataAccess dataAccess,
        IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService) : base(dataAccess)
    {
        _repository = new Z9DevRepository(dataAccess);
        _hubContext = hubContext;
        _extensionService = extensionService;
    }

    protected override DevType FilterDevType => DevType.Sensor;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll())
            .Where(d => d.DevType == DevType.Sensor && d.Enabled)
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

    public override async Task<IActionResult> Save([FromBody] FlexDev body)
    {
        var poolDev = await _repository.Get(body.Unid ?? 0);
        if (poolDev == null)
            return NotFound();

        poolDev.Name = body.Name;
        poolDev.Enabled = true;
        await _repository.Upsert(poolDev);

        await _hubContext.Clients.All.SendAsync(Methods.InputInserted, poolDev.Unid);

        return Ok(new FlexInstanceResponse<FlexDev> { Instance = ToFlex(poolDev) });
    }

    public override async Task<IActionResult> Delete(string id)
    {
        var result = await base.Delete(id);

        if (int.TryParse(id, out var unid))
            await _hubContext.Clients.All.SendAsync(Methods.InputDeleted, unid);

        return result;
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available()
    {
        var available = await _repository.GetAvailableByDevType(DevType.Sensor);
        var list = available.Select(ToFlex).ToList();
        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = 0,
            Max = list.Count,
            Count = list.Count,
            InstanceList = list,
        });
    }

    [HttpGet("state/{id:int}")]
    public async Task<IActionResult> GetState(int id)
    {
        var dev = await _repository.Get(id);
        if (dev == null)
            return NotFound();

        var extensionId = await _repository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            return Ok(new { state = (bool?)null });

        var state = await _extensionService.GetMonitorPoint(extensionId.Value, dev.ExternalId).GetState();
        return Ok(new { state });
    }
}

[ApiController]
[Route("flex/actuator")]
public class FlexActuatorController : FlexDevTypeControllerBase
{
    private readonly Z9DevRepository _repository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;
    private readonly DevStateService _devStateService;

    public FlexActuatorController(IDataAccess dataAccess,
        IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService,
        DevStateService devStateService) : base(dataAccess)
    {
        _repository = new Z9DevRepository(dataAccess);
        _hubContext = hubContext;
        _extensionService = extensionService;
        _devStateService = devStateService;
    }

    protected override DevType FilterDevType => DevType.Actuator;

    [HttpGet("list")]
    public override async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll())
            .Where(d => d.DevType == DevType.Actuator && d.Enabled)
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

    public override async Task<IActionResult> Save([FromBody] FlexDev body)
    {
        var poolDev = await _repository.Get(body.Unid ?? 0);
        if (poolDev == null)
            return NotFound();

        poolDev.Name = body.Name;
        poolDev.Enabled = true;
        await _repository.Upsert(poolDev);

        await _hubContext.Clients.All.SendAsync(Methods.OutputInserted, poolDev.Unid);

        return Ok(new FlexInstanceResponse<FlexDev> { Instance = ToFlex(poolDev) });
    }

    public override async Task<IActionResult> Delete(string id)
    {
        var result = await base.Delete(id);

        if (int.TryParse(id, out var unid))
            await _hubContext.Clients.All.SendAsync(Methods.OutputDeleted, unid);

        return result;
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available()
    {
        var available = await _repository.GetAvailableByDevType(DevType.Actuator);
        var list = available.Select(ToFlex).ToList();
        return Ok(new FlexListResponse<FlexDev>
        {
            Offset = 0,
            Max = list.Count,
            Count = list.Count,
            InstanceList = list,
        });
    }

    [HttpGet("state/{id:int}")]
    public async Task<IActionResult> GetState(int id)
    {
        var dev = await _repository.Get(id);
        if (dev == null)
            return NotFound();

        var extensionId = await _repository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            return Ok(new { state = (bool?)null });

        var state = await _extensionService.GetControlPoint(extensionId.Value, dev.ExternalId).GetState();
        return Ok(new { state });
    }

    [HttpPost("state/{id:int}")]
    public async Task<IActionResult> SetState(int id, [FromQuery] bool state)
    {
        var dev = await _repository.Get(id);
        if (dev == null)
            return NotFound();

        var extensionId = await _repository.GetExtensionId(dev);
        if (!extensionId.HasValue)
            return NotFound();

        await _extensionService.GetControlPoint(extensionId.Value, dev.ExternalId).SetState(state);

        await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, dev.Unid, state);

        _devStateService.UpdateAspect(dev.Unid, DevAspect.Primary,
            s => s.ActivityState = state ? ActivityState.Active : ActivityState.Inactive);

        return Ok(new FlexVoid());
    }
}

[ApiController]
[Route("flex/nodeDev")]
public class FlexNodeDevController : FlexDevTypeControllerBase
{
    public FlexNodeDevController(IDataAccess dataAccess) : base(dataAccess) { }
    protected override DevType FilterDevType => DevType.Reserved0;
}
