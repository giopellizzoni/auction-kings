using AuctionKings.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuctionKings.UnitTests.API.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldCallNext()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/auctions";
        var middleware = new RequestLoggingMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<RequestLoggingMiddleware>.Instance
        );

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ShouldRethrow()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/auctions";
        var middleware = new RequestLoggingMiddleware(
            _ => throw new InvalidOperationException("downstream failure"),
            NullLogger<RequestLoggingMiddleware>.Instance
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context)
        );
    }
}
