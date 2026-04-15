using AuctionKings.API.Middleware;
using Microsoft.AspNetCore.Http;

namespace AuctionKings.UnitTests.API.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    private const string HeaderName = CorrelationIdMiddleware.HeaderName;

    [Fact]
    public async Task CorrelationIdMiddleware_NoHeader_ShouldGenerate()
    {
        var httpContext = new DefaultHttpContext();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);

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
        var middleware = new CorrelationIdMiddleware(next);

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(existingId, httpContext.Response.Headers[HeaderName].ToString());
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WithHeader_ShouldAddToResponseHeaders()
    {
        var existingId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = existingId;
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(existingId, httpContext.Response.Headers[HeaderName].ToString());
    }
}
