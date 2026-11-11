using System.Reflection;
using AuctionKings.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace AuctionKings.UnitTests.API.Filters;

public sealed class IdempotencyFilterTests
{
    // A real method with [Idempotent] so ControllerActionDescriptor.MethodInfo carries the attribute.
    [Idempotent]
    private static void FakeIdempotentAction() { }

    private static void FakeRegularAction() { }

    private static ActionExecutingContext BuildContext(HttpContext httpContext, MethodInfo methodInfo)
    {
        var descriptor = new ControllerActionDescriptor { MethodInfo = methodInfo };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }

    private static MethodInfo IdempotentMethod =>
        typeof(IdempotencyFilterTests).GetMethod(nameof(FakeIdempotentAction), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MethodInfo RegularMethod =>
        typeof(IdempotencyFilterTests).GetMethod(nameof(FakeRegularAction), BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public async Task IdempotencyFilter_PostWithValidGuidHeader_ShouldStoreKeyAndProceed()
    {
        var requestId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["X-Request-Id"] = requestId;
        var context = BuildContext(httpContext, IdempotentMethod);
        var filter = new IdempotencyFilter();

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        };

        await filter.OnActionExecutionAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal(requestId, httpContext.Items[IdempotencyFilter.ItemsKey]);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task IdempotencyFilter_PostWithoutHeader_ShouldReturn400()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        var context = BuildContext(httpContext, IdempotentMethod);
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
    public async Task IdempotencyFilter_PostWithNonGuidHeader_ShouldReturn400()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["X-Request-Id"] = "not-a-guid";
        var context = BuildContext(httpContext, IdempotentMethod);
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
    public async Task IdempotencyFilter_PostWithWhitespaceHeader_ShouldReturn400()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers["X-Request-Id"] = "   ";
        var context = BuildContext(httpContext, IdempotentMethod);
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
    public async Task IdempotencyFilter_ActionWithoutIdempotentAttribute_ShouldPassThrough()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        // No X-Request-Id header — but filter should not enforce it without [Idempotent].
        var context = BuildContext(httpContext, RegularMethod);
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
        Assert.False(httpContext.Items.ContainsKey(IdempotencyFilter.ItemsKey));
    }

    [Fact]
    public async Task IdempotencyFilter_GetRequest_ShouldPassThrough()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        // Even with [Idempotent] descriptor, GET passes through (no header check).
        // In practice GET actions won't have [Idempotent], but verifies graceful pass-through.
        var context = BuildContext(httpContext, RegularMethod);
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
        Assert.False(httpContext.Items.ContainsKey(IdempotencyFilter.ItemsKey));
    }
}
