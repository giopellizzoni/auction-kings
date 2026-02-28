using AuctionKings.Application.Behaviors;
using AuctionKings.UnitTests.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuctionKings.UnitTests.Application;

public sealed class LoggingBehaviorTests
{
    private sealed record TestRequest(string Value) : IRequest<string>;

    [Fact]
    public async Task LoggingBehavior_ShouldLogRequestAndResponse()
    {
        var logger = new TestLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        var request = new TestRequest("test");
        RequestHandlerDelegate<string> next = _ => Task.FromResult("done");

        await behavior.Handle(request, next, CancellationToken.None);

        Assert.True(logger.HasLog(LogLevel.Information, "TestRequest"));
        Assert.True(logger.Logs.Count >= 2);
    }

    [Fact]
    public async Task LoggingBehavior_ShouldReturnHandlerResponse()
    {
        var logger = new TestLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        var request = new TestRequest("test");
        RequestHandlerDelegate<string> next = _ => Task.FromResult("expected-value");

        var result = await behavior.Handle(request, next, CancellationToken.None);

        Assert.Equal("expected-value", result);
    }
}
