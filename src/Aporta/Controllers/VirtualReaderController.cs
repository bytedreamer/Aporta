using Aporta.Core.Services;
using Aporta.Drivers.Virtual.Shared;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Aporta.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VirtualReaderController : ControllerBase
    {

        private readonly ILogger<ExtensionsController> _logger;
        private readonly ExtensionService _extensionService;

        public VirtualReaderController(ExtensionService extensionService)
        {
            _extensionService = extensionService;
        }

        [HttpPut]
        public async Task Put([FromBody] Reader reader)
        {
            //_extensionService.PerformAction()
            //await _doorConfigurationService.Insert(door);
        }

        [HttpDelete("{doorId:int}")]
        public async Task Delete(int doorId)
        {
            //await _doorConfigurationService.Delete(doorId);
        }

    }
}
