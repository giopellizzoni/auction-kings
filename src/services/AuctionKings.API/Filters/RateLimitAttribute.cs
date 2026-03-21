using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuctionKings.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimitAttribute(string policy = "default", int retryAfterSeconds = 60)
    : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var rateLimiter = serviceProvider.GetRequiredService<IRateLimiter>();
        return new RateLimitFilter(rateLimiter, policy, retryAfterSeconds);
    }
}

internal sealed class RateLimitFilter(IRateLimiter rateLimiter, string policy, int retryAfterSeconds)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var identity =
            context.HttpContext.User.Identity?.Name
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        var allowed = await rateLimiter.IsAllowedAsync($"rate:{policy}:{identity}");
        if (!allowed)
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            return;
        }

        await next();
    }
}
