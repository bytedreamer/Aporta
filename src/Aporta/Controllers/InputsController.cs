using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InputsController : ControllerBase
{
    private readonly InputService _inputService;

    public InputsController(InputService inputService)
    {
        _inputService = inputService;
    }
        
    [HttpGet]
    public async Task<IEnumerable<Input>> Get()
    {
        return await _inputService.GetAll();
    }
        
    [HttpGet("{inputId}")]
    public async Task<Input> Get(int inputId)
    {
        return await _inputService.Get(inputId);
    }

    [HttpPut]
    public async Task Put([FromBody]Input input)
    {
        await _inputService.Insert(input);
    }
        
    [HttpDelete("{inputId:int}")]
    public async Task Delete(int inputId)
    {
        await _inputService.Delete(inputId);
    }
        
    [HttpGet("available")]
    public async Task<IEnumerable<Endpoint>> Available()
    {
        return await _inputService.AvailableMonitorPoints();
    }
        
    [HttpGet("get/{inputId:int}")]
    public async Task<bool?> GetState(int inputId)
    {
        return await _inputService.GetState(inputId);
    }

    [HttpPost("set/{inputId:int}")]
    public async Task<IActionResult> SetState(int inputId, [FromQuery] bool state)
    {
        await _inputService.SetState(inputId, state);

        return NoContent();
    }
}