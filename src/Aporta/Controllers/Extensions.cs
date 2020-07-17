using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.Hubs;
using Aporta.Core.Models;
using Aporta.Core.Services;
using Aporta.Shared.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExtensionsController : Controller
    {
        private readonly ILogger<ExtensionsController> _logger;
        private readonly ExtensionService _extensionService;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;

        public ExtensionsController(ExtensionService extensionService, IHubContext<DataChangeNotificationHub> hubContext,
            ILogger<ExtensionsController> logger)
        {
            _logger = logger;
            if (extensionService != null) _extensionService = extensionService;
            if (hubContext != null) _hubContext = hubContext;
        }

        [HttpGet]
        public IEnumerable<ExtensionHost> Get()
        {
            return _extensionService.Extensions;
        }
        
        [HttpGet("{extensionId:Guid}")]
        public ExtensionHost Get(Guid extensionId)
        {
            return _extensionService.Extensions.First(extension => extension.Id == extensionId);
        }

        [HttpPost("{extensionId:Guid}")]
        public async Task<ActionResult> SetEnabled(Guid extensionId, [FromQuery] bool enabled)
        {
            bool success = true;
            try
            {
                await _extensionService.EnableExtension(extensionId, enabled);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Unable to update extension {extensionId}");
                success = false;
            }
            
            await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);

            return success ? (ActionResult) NoContent() : Problem();
        }

        [HttpPost("{extensionId:Guid}/configuration")]
        public async Task<ActionResult> UpdateConfiguration(Guid extensionId, [FromBody] dynamic configuration)
        {
            bool success = true;
            try
            {
                await _extensionService.UpdateConfiguration(extensionId, JsonSerializer.Serialize(configuration));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Unable to update configuration for extension {extensionId}");
                success = false;
            }

            await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);

            return success ? (ActionResult) NoContent() : Problem();
        }

        [HttpPost("{extensionId:Guid}/action/{actionType}")]
        public async Task<ActionResult> PerformAction(Guid extensionId, string actionType, [FromBody] dynamic parameter)
        {
            bool success = true;
            string result = string.Empty;
            try
            {
                result = await _extensionService.PerformAction(extensionId, actionType,
                    JsonSerializer.Serialize(parameter));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Unable to perform action {actionType} for extension {extensionId}");
                success = false;
            }

            return success ? (ActionResult) Content(result) : Problem();
        }
    }
}