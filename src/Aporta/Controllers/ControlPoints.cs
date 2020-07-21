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
        private readonly ControlPointService _controlPointService;

        public ControlPoints(ControlPointService controlPointService)
        {
            _controlPointService = controlPointService;
        }

        [HttpPost]
        public async Task<IActionResult> Set(bool state)
        {
            await _controlPointService.SetOutput(Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5"), "1:O0", state);

            return NoContent();
        }
    }
}