using AuctionKings.API.Filters;
using AuctionKings.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Filters;

public sealed class GlobalExceptionFilterTests
{
    private static ExceptionContext BuildContext(Exception exception)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }

    private static GlobalExceptionFilter BuildFilter(bool isDevelopment = false)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(isDevelopment ? "Development" : "Production");
        return new GlobalExceptionFilter(NullLogger<GlobalExceptionFilter>.Instance, env);
    }

    [Fact]
    public void OnException_ValidationException_Returns400WithErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Title"] = ["Title is required", "Title must be at least 3 chars"]
        };
        var context = BuildContext(new ValidationException(errors));
        var filter = BuildFilter();

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(400, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(400, problem.Status);
        Assert.True(context.ExceptionHandled);
        Assert.True(problem.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public void OnException_ConcurrencyException_Returns409()
    {
        var context = BuildContext(new ConcurrencyException());
        var filter = BuildFilter();

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Conflict", problem.Title);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_DuplicateCommandException_Returns409()
    {
        var context = BuildContext(new DuplicateCommandException("key-123"));
        var filter = BuildFilter();

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Duplicate Request", problem.Title);
        Assert.Equal("A duplicate request was detected. The operation has already been processed.", problem.Detail);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_DuplicateCommandException_InDevelopment_ExposesDetail()
    {
        var context = BuildContext(new DuplicateCommandException("key-123"));
        var filter = BuildFilter(isDevelopment: true);

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Contains("key-123", problem.Detail);
    }

    [Fact]
    public void OnException_UnhandledException_InProduction_Returns500WithGenericDetail()
    {
        var context = BuildContext(new InvalidOperationException("boom"));
        var filter = BuildFilter();

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(500, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("application/problem+json", result.ContentTypes.First());
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void OnException_UnhandledException_InDevelopment_ExposesDetail()
    {
        var context = BuildContext(new InvalidOperationException("secret details"));
        var filter = BuildFilter(isDevelopment: true);

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Contains("secret details", problem.Detail);
    }
}
