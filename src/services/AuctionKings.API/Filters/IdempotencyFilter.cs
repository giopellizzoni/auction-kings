using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuctionKings.API.Filters;

public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private const string HeaderName = "X-Request-Id";
    private const string ItemsKey = "IdempotencyKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            context.Result = new BadRequestObjectResult(new
            {
                code = "Idempotency.MissingKey",
                message = "X-Request-Id header is required"
            });
            return;
        }

        context.HttpContext.Items[ItemsKey] = values.ToString();

        await next();
    }
}
