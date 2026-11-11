using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuctionKings.API.Filters;

public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private const string HeaderName = "X-Request-Id";
    internal const string ItemsKey = "IdempotencyKey";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var hasIdempotentAttribute = context.ActionDescriptor is ControllerActionDescriptor cad
            && cad.MethodInfo.GetCustomAttributes(typeof(IdempotentAttribute), false).Length > 0;

        if (!hasIdempotentAttribute)
        {
            await next();
            return;
        }

        if (
            !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString())
        )
        {
            context.Result = new BadRequestObjectResult(
                new { code = "Idempotency.MissingKey", message = "X-Request-Id header is required" }
            );
            return;
        }

        var rawValue = values.ToString();
        if (!Guid.TryParse(rawValue, out _))
        {
            context.Result = new BadRequestObjectResult(
                new { code = "Idempotency.InvalidKey", message = "X-Request-Id must be a valid GUID" }
            );
            return;
        }

        context.HttpContext.Items[ItemsKey] = rawValue;

        await next();
    }
}
