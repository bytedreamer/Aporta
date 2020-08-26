using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoorsController : ControllerBase
    {
        private readonly DoorService _doorService;

        public DoorsController(DoorService doorService)
        {
            _doorService = doorService;
        }
        
        [HttpGet]
        public async Task<IEnumerable<Door>> Get()
        {
            return await _doorService.GetAll();
        }
        
        [HttpGet("{doorId:int}")]
        public async Task<Door> Get(int doorId)
        {
            return await _doorService.Get(doorId);
        }

        [HttpPut]
        public async Task Put([FromBody]Door door)
        {
            await _doorService.Insert(door);
        }
        
        [HttpDelete("{doorId:int}")]
        public async Task Delete(int doorId)
        {
            await _doorService.Delete(doorId);
        }
        
        [HttpGet("available")]
        public async Task<IEnumerable<Endpoint>> Available()
        {
            return await _doorService.AvailableAccessPoints();
        }
    }
}