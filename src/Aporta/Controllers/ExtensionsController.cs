using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExtensionsController(
    ExtensionService extensionService,
    ILogger<ExtensionsController> logger)
    : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Extension>> Get()
    {
        try
        {
            return new ActionResult<IEnumerable<Extension>>(extensionService.GetExtensions());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to get extensions");
            return Problem(exception.Message);
        }
    }
        
    [HttpGet("{extensionId:Guid}")]
    public ActionResult<Extension> Get(Guid extensionId)
    {
        try
        {
            return new ActionResult<Extension>(extensionService.GetExtension(extensionId));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to get extension {ExtensionId}", extensionId);
            return Problem(exception.Message);
        }
    }

    [HttpPost("{extensionId:Guid}")]
    public async Task<ActionResult> SetEnabled(Guid extensionId, [FromQuery] bool enabled)
    {
        try
        {
            await extensionService.EnableExtension(extensionId, enabled);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to update extension {ExtensionId}", extensionId);
            return Problem(exception.Message);
        }

        return NoContent();
    }

    [HttpPost("{extensionId:Guid}/action/{actionType}")]
    public async Task<ActionResult> PerformAction(Guid extensionId, string actionType, [FromBody] dynamic parameter)
    {
        try
        {
            return Content(await extensionService.PerformAction(extensionId, actionType,
                JsonSerializer.Serialize(parameter)));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to perform action {ActionType} for extension {ExtensionId}", actionType, extensionId);
            return Problem(exception.Message);
        }
    }
}