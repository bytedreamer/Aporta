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
}
