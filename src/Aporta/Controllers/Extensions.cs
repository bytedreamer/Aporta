using System.Collections.Generic;
using Aporta.Core.Models;
using Aporta.Core.Services;
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
    }
}