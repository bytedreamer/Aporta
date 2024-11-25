using Aporta.Core.Services;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aporta.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController
    {
        private readonly UserService _userService;

        public UserController(UserService peopleService)
        {
            _userService = peopleService;
        }

        [HttpGet]
        public async Task<IEnumerable<User>> Get()
        {
            return await _userService.GetAll();
        }

        [HttpGet("{userId}")]
        public async Task<User> Get(int userId)
        {
            return await _userService.Get(userId);
        }

        [HttpGet("login/{password}")]
        public async Task<LoginResult> Login(string password)
        {
            return await _userService.Login(password);
        }

        [HttpPut]
        public async Task Put([FromBody] User user)
        {
            await _userService.Insert(user);
        }

        [HttpDelete("{userId:int}")]
        public async Task Delete(int userId)
        {
            await _userService.Delete(userId);
        }
    }
}
