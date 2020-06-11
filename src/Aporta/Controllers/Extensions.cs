using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Hubs;
using Aporta.Core.Models;
using Aporta.Core.Services;
using Aporta.Shared.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExtensionsController : Controller
    {
        private readonly IMainService _mainService;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;

        public ExtensionsController(IMainService mainService, IHubContext<DataChangeNotificationHub> hubContext)
        {
            if (mainService != null) _mainService = mainService;
            if (hubContext != null) _hubContext = hubContext;
        }

        [HttpGet]
        public IEnumerable<ExtensionHost> Get()
        {
            return _mainService.Extensions;
        }

        [HttpPost("{extensionId}")]
        public async Task<ActionResult> SetEnabled(Guid extensionId, [FromQuery] bool enabled)
        {
            await _mainService.SetExtensionEnable(extensionId, enabled);

            await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged);

            return NoContent();
        }
    }
}