using System;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Extensions.Endpoint;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ControlPoints : Controller
    {
        private readonly IMainService _mainService;

        public ControlPoints(IMainService mainService)
        {
            _mainService = mainService;
        }

        [HttpPost]
        public async Task<IActionResult> Set(bool state)
        {
            var driver = _mainService.Driver(Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5"));
            
            await driver.Endpoints.Cast<IControlPoint>().First().Set(state);
            
            return NoContent();
        }
    }
}