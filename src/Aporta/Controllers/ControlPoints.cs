using System;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ControlPoints : Controller
    {
        private readonly OutputService _outputService;

        public ControlPoints(OutputService outputService)
        {
            _outputService = outputService;
        }

        [HttpPost]
        public async Task<IActionResult> Set(bool state)
        {
            await _outputService.Set(Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5"), "1:O0", state);

            return NoContent();
        }
    }
}