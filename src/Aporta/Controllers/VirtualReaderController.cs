using Aporta.Core.Services;
using Aporta.Drivers.Virtual.Shared;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Aporta.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VirtualReaderController : ControllerBase
    {

        private readonly ILogger<ExtensionsController> _logger;
        private readonly ExtensionService _extensionService;

        public VirtualReaderController(ExtensionService extensionService, ILogger<ExtensionsController> logger)
        {
            _extensionService = extensionService;
            _logger = logger;
        }

        [HttpPut]
        public async Task Put([FromBody] Reader readerToAdd)
        {
            var readerToRemoveSerialized = JsonConvert.SerializeObject(readerToAdd);
            //await _extensionService.PerformAction();
            //await _doorConfigurationService.Insert(door);
        }

        [HttpDelete]
        public async Task Delete([FromBody] Reader readerToRemove)
        {
            var readerToRemoveSerialized = JsonConvert.SerializeObject(readerToRemove);
            //await _doorConfigurationService.Delete(doorId);
        }

    }
}
