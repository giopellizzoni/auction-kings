using Serilog.Context;

namespace AuctionKings.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Accept the inbound header only when it is a well-formed GUID; otherwise generate a
        // fresh one. This prevents log-injection via a crafted X-Correlation-Id value.
        string correlationId;
        if (context.Request.Headers.TryGetValue(HeaderName, out var value)
            && Guid.TryParse(value.ToString(), out _))
        {
            correlationId = value.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
