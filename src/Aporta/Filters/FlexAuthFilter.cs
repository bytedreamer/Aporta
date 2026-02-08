using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Aporta.Filters;

public class FlexAuthFilter : IActionFilter
{
    private readonly FlexSessionService _sessionService;

    public FlexAuthFilter(FlexSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path.Value;

        // Skip auth check for authenticate endpoint
        if (path != null && path.StartsWith("/flex/authenticate"))
            return;

        // Only apply to /flex/ routes
        if (path == null || !path.StartsWith("/flex/"))
            return;

        var token = context.HttpContext.Request.Headers["sessionToken"].ToString();
        if (!_sessionService.ValidateSession(token))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
