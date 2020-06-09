using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Models;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExtensionsController : Controller
    {
        private readonly IMainService _mainService;

        public ExtensionsController(IMainService mainService)
        {
            _mainService = mainService;
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

            return NoContent();
        }
    }
}