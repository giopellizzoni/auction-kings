using System.Diagnostics;

namespace AuctionKings.API.Middleware;

public sealed partial class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            LogRequest(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds
            );
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms")]
    private static partial void LogRequest(ILogger logger, string method, string path, int statusCode, long elapsedMs);
}
