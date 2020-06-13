using System.Threading.Tasks;
using Aporta.Core.Services;
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
            await _mainService.SetOutput(state);
            
            return NoContent();
        }
    }
}