using AuctionKings.API.Middleware;
using Microsoft.AspNetCore.Http;

namespace AuctionKings.UnitTests.API.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldAddAllSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("", context.Response.Headers["X-XSS-Protection"].ToString());
        Assert.Equal("max-age=31536000; includeSubDomains",
            context.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal("strict-origin-when-cross-origin",
            context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("default-src 'none'",
            context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNext()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
