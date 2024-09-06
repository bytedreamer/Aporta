using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoorsController : ControllerBase
{
    private readonly DoorConfigurationService _doorConfigurationService;

    public DoorsController(DoorConfigurationService doorConfigurationService)
    {
        _doorConfigurationService = doorConfigurationService;
    }
        
    [HttpGet]
    public async Task<IEnumerable<Door>> Get()
    {
        return await _doorConfigurationService.GetAll();
    }
        
    [HttpGet("{doorId:int}")]
    public async Task<Door> Get(int doorId)
    {
        return await _doorConfigurationService.Get(doorId);
    }

    [HttpPut]
    public async Task Put([FromBody]Door door)
    {
        await _doorConfigurationService.Insert(door);
    }
        
    [HttpDelete("{doorId:int}")]
    public async Task Delete(int doorId)
    {
        await _doorConfigurationService.Delete(doorId);
    }
        
    [HttpGet("available")]
    public async Task<IEnumerable<Endpoint>> Available()
    {
        return await _doorConfigurationService.AvailableAccessPoints();
    }

    [HttpGet("endpointsavailable")]
    public async Task<IEnumerable<Endpoint>> AvailableEndPoints()
    {
        return await _doorConfigurationService.AvailableEndPoints();
    }
}