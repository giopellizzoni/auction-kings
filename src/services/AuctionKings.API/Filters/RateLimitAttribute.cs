using System.Globalization;
using System.Security.Claims;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuctionKings.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimitAttribute(
    string policy = "default",
    int retryAfterSeconds = 60,
    bool failOpen = true) : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var rateLimiter = serviceProvider.GetRequiredService<IRateLimiter>();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitOptions>>().Value;
        return new RateLimitFilter(rateLimiter, options.MaxRequests, policy, retryAfterSeconds, failOpen);
    }
}

internal sealed class RateLimitFilter(
    IRateLimiter rateLimiter,
    int maxRequests,
    string policy,
    int retryAfterSeconds,
    bool failOpen) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Prefer authenticated user ID over IP to avoid blocking shared NAT addresses.
        var identity =
            context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        var result = await rateLimiter.IsAllowedAsync($"rate:{policy}:{identity}");

        if (result.IsError && !failOpen)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        if (!result.IsAllowed)
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            SetRateLimitHeaders(context.HttpContext.Response, maxRequests, 0);
            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            return;
        }

        SetRateLimitHeaders(context.HttpContext.Response, maxRequests, Math.Max(0, maxRequests - result.CurrentCount));

        await next();
    }

    private static void SetRateLimitHeaders(HttpResponse response, int limit, int remaining)
    {
        response.Headers["X-RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Remaining"] = remaining.ToString(CultureInfo.InvariantCulture);
    }
}
