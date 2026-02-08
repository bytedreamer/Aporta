using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers.Flex;

[ApiController]
public class FlexAuthenticateController : ControllerBase
{
    private readonly FlexSessionService _sessionService;

    public FlexAuthenticateController(FlexSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpPost("flex/authenticate")]
    public IActionResult Authenticate([FromBody] FlexAuthenticateRequest request)
    {
        var result = _sessionService.Authenticate(request.Username, request.Password);
        return Ok(result);
    }

    [HttpGet("flex/terminate")]
    public IActionResult Terminate()
    {
        var token = Request.Headers["sessionToken"].ToString();
        _sessionService.Terminate(token);
        return Ok(new FlexVoid());
    }
}
