using AuctionKings.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace AuctionKings.UnitTests.API.Filters;

public sealed class IdempotencyFilterTests
{
    private static ActionExecutingContext BuildContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }

    [Fact]
    public async Task IdempotencyFilter_PostWithHeader_ShouldStoreKeyAndProceed()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["X-Request-Id"] = "key-abc";
        var context = BuildContext(httpContext);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("key-abc", httpContext.Items["IdempotencyKey"]);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task IdempotencyFilter_PostWithoutHeader_ShouldReturn400()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        var context = BuildContext(httpContext);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.False(nextCalled);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task IdempotencyFilter_PutWithHeader_ShouldStoreKeyAndProceed()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Put;
        httpContext.Request.Headers["X-Request-Id"] = "key-put";
        var context = BuildContext(httpContext);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("key-put", httpContext.Items["IdempotencyKey"]);
    }

    [Fact]
    public async Task IdempotencyFilter_PostWithWhitespaceHeader_ShouldReturn400()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["X-Request-Id"] = "   ";
        var context = BuildContext(httpContext);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.False(nextCalled);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public async Task IdempotencyFilter_GetRequest_ShouldPassThrough()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        var context = BuildContext(httpContext);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.True(nextCalled);
        Assert.Null(context.Result);
        Assert.False(httpContext.Items.ContainsKey("IdempotencyKey"));
    }
}
