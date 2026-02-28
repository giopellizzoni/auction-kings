using AuctionKings.Application.Behaviors;
using AuctionKings.UnitTests.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuctionKings.UnitTests.Application;

public sealed class PerformanceBehaviorTests
{
    private sealed record TestRequest(string Value) : IRequest<string>;

    [Fact]
    public async Task PerformanceBehavior_FastRequest_ShouldNotLogWarning()
    {
        var logger = new TestLogger<PerformanceBehavior<TestRequest, string>>();
        var behavior = new PerformanceBehavior<TestRequest, string>(logger);

        var request = new TestRequest("fast");
        RequestHandlerDelegate<string> next = _ => Task.FromResult("done");

        await behavior.Handle(request, next, CancellationToken.None);

        Assert.False(logger.HasLog(LogLevel.Warning, "Long running request"));
    }

    [Fact]
    public async Task PerformanceBehavior_SlowRequest_ShouldLogWarning()
    {
        var logger = new TestLogger<PerformanceBehavior<TestRequest, string>>();
        var behavior = new PerformanceBehavior<TestRequest, string>(logger);

        var request = new TestRequest("slow");
        RequestHandlerDelegate<string> next = async ct =>
        {
            await Task.Delay(600, ct);
            return "done";
        };

        await behavior.Handle(request, next, CancellationToken.None);

        Assert.True(logger.HasLog(LogLevel.Warning, "Long running request"));
        Assert.True(logger.HasLog(LogLevel.Warning, "TestRequest"));
    }
}
