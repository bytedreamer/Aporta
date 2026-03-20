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
        var controllerType = context.Controller.GetType();

        // Only apply to Flex controllers (namespace Aporta.Controllers.Flex)
        if (controllerType.Namespace == null ||
            !controllerType.Namespace.StartsWith("Aporta.Controllers.Flex"))
            return;

        // Skip auth check for authenticate/terminate controller
        if (controllerType.Name == "FlexAuthenticateController")
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
