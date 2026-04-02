using AuctionKings.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-Id";

    private static ILogger<CorrelationIdMiddleware> BuildLogger()
    {
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        logger.BeginScope(Arg.Any<Dictionary<string, object>>())
              .Returns(Substitute.For<IDisposable>());
        return logger;
    }

    [Fact]
    public async Task CorrelationIdMiddleware_NoHeader_ShouldGenerate()
    {
        var httpContext = new DefaultHttpContext();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, BuildLogger());

        await middleware.InvokeAsync(httpContext);

        Assert.True(httpContext.Response.Headers.ContainsKey(HeaderName));
        Assert.True(Guid.TryParse(httpContext.Response.Headers[HeaderName].ToString(), out _));
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WithHeader_ShouldPropagate()
    {
        var existingId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = existingId;
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, BuildLogger());

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(existingId, httpContext.Response.Headers[HeaderName].ToString());
    }
}
