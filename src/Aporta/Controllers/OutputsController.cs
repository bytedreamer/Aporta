using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutputsController : ControllerBase
{
    private readonly OutputService _outputService;

    public OutputsController(OutputService outputService)
    {
        _outputService = outputService;
    }
        
    [HttpGet]
    public async Task<IEnumerable<Output>> Get()
    {
        return await _outputService.GetAll();
    }
        
    [HttpGet("{outputId}")]
    public async Task<Output> Get(int outputId)
    {
        return await _outputService.Get(outputId);
    }

    [HttpPut]
    public async Task Put([FromBody]Output output)
    {
        await _outputService.Insert(output);
    }
        
    [HttpDelete("{outputId:int}")]
    public async Task Delete(int outputId)
    {
        await _outputService.Delete(outputId);
    }
        
    [HttpGet("available")]
    public async Task<IEnumerable<Endpoint>> Available()
    {
        return await _outputService.AvailableControlPoints();
    }

    [HttpGet("outputendpoints")]
    public async Task<IEnumerable<Endpoint>> AllOutputEndpoints()
    {
        return await _outputService.AllOutputEndPoints();
    }

    [HttpGet("get/{outputId:int}")]
    public async Task<bool?> GetState(int outputId)
    {
        return await _outputService.GetState(outputId);
    }
        
    [HttpPost("set/{outputId:int}")]
    public async Task<IActionResult> SetState(int outputId, [FromQuery]bool state)
    {
        await _outputService.SetState(outputId, state);

        return NoContent();
    }
}