using System.Security.Claims;
using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Hubs;

public sealed class HubThrottlingFilterTests
{
    private static readonly HubThrottlingOptions Limits = new()
    {
        MaxInvocations = 7,
        Window = TimeSpan.FromSeconds(3),
        MaxConnectionsPerUser = 2,
    };

    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();
    private readonly IConnectionTracker _tracker = Substitute.For<IConnectionTracker>();
    private readonly HubThrottlingFilter _filter;

    public HubThrottlingFilterTests()
    {
        _filter = new HubThrottlingFilter(
            _rateLimiter,
            _tracker,
            Options.Create(Limits),
            NullLogger<HubThrottlingFilter>.Instance);

        Allow();
    }

    // ---- InvokeMethodAsync ----

    [Fact]
    public async Task InvokeMethod_WithinLimit_ShouldCallHubMethodAndReturnItsResult()
    {
        var userId = Guid.NewGuid();
        var nextCalls = 0;

        var result = await _filter.InvokeMethodAsync(
            Invocation(userId, "conn-1"),
            _ =>
            {
                nextCalls++;
                return ValueTask.FromResult<object?>("hub-result");
            });

        Assert.Equal("hub-result", result);
        Assert.Equal(1, nextCalls);
        await _rateLimiter.Received(1).IsAllowedAsync(
            $"rate:hub:{userId}", Limits.MaxInvocations, Limits.Window, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeMethod_OverLimit_ShouldThrowHubException_AndSkipHubMethod()
    {
        _rateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(IsAllowed: false, CurrentCount: Limits.MaxInvocations));
        var nextCalls = 0;

        var ex = await Assert.ThrowsAsync<HubException>(() => _filter
            .InvokeMethodAsync(
                Invocation(Guid.NewGuid(), "conn-1"),
                _ =>
                {
                    nextCalls++;
                    return ValueTask.FromResult<object?>(null);
                })
            .AsTask());

        Assert.Contains("Rate limit exceeded", ex.Message);
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task InvokeMethod_SameUserOnDifferentConnections_ShouldShareOneBudget()
    {
        var userId = Guid.NewGuid();

        await _filter.InvokeMethodAsync(Invocation(userId, "conn-1"), Next);
        await _filter.InvokeMethodAsync(Invocation(userId, "conn-2"), Next);

        await _rateLimiter.Received(2).IsAllowedAsync(
            $"rate:hub:{userId}", Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeMethod_DifferentUsers_ShouldUseSeparateBudgets()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await _filter.InvokeMethodAsync(Invocation(first, "conn-1"), Next);
        await _filter.InvokeMethodAsync(Invocation(second, "conn-2"), Next);

        await _rateLimiter.Received(1).IsAllowedAsync(
            $"rate:hub:{first}", Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _rateLimiter.Received(1).IsAllowedAsync(
            $"rate:hub:{second}", Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeMethod_WithoutUserId_ShouldFallBackToConnectionId()
    {
        await _filter.InvokeMethodAsync(Invocation(userId: null, "conn-9"), Next);

        await _rateLimiter.Received(1).IsAllowedAsync(
            "rate:hub:conn-9", Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeMethod_WhenLimiterErrors_ShouldFailOpen()
    {
        // RedisRateLimiter reports Redis failures as IsAllowed = true with IsError = true.
        _rateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(IsAllowed: true, CurrentCount: 0, IsError: true));
        var nextCalls = 0;

        await _filter.InvokeMethodAsync(
            Invocation(Guid.NewGuid(), "conn-1"),
            _ =>
            {
                nextCalls++;
                return ValueTask.FromResult<object?>(null);
            });

        Assert.Equal(1, nextCalls);
    }

    // ---- OnConnectedAsync ----

    [Fact]
    public async Task OnConnected_WithinConnectionCap_ShouldNotAbort()
    {
        var userId = Guid.NewGuid();
        var caller = Caller(userId, "conn-2");
        _tracker.GetConnectionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(["conn-1", "conn-2"]); // exactly at the cap of 2

        await _filter.OnConnectedAsync(Lifetime(caller), _ => Task.CompletedTask);

        caller.DidNotReceive().Abort();
    }

    [Fact]
    public async Task OnConnected_OverConnectionCap_ShouldAbortTheNewConnection()
    {
        var userId = Guid.NewGuid();
        var caller = Caller(userId, "conn-3");
        _tracker.GetConnectionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(["conn-1", "conn-2", "conn-3"]);

        await _filter.OnConnectedAsync(Lifetime(caller), _ => Task.CompletedTask);

        caller.Received(1).Abort();
    }

    [Fact]
    public async Task OnConnected_ShouldRunHubOnConnectedBeforeCounting()
    {
        // The hub's OnConnectedAsync registers the connection; counting first would miss it.
        var userId = Guid.NewGuid();
        var order = new List<string>();
        _tracker.GetConnectionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("count");
                return Task.FromResult<IReadOnlyCollection<string>>(["conn-1"]);
            });

        await _filter.OnConnectedAsync(
            Lifetime(Caller(userId, "conn-1")),
            _ =>
            {
                order.Add("hub");
                return Task.CompletedTask;
            });

        Assert.Equal(["hub", "count"], order);
    }

    [Fact]
    public async Task OnConnected_WithoutUserId_ShouldNotQueryTheTracker()
    {
        var caller = Caller(userId: null, "conn-1");

        await _filter.OnConnectedAsync(Lifetime(caller), _ => Task.CompletedTask);

        await _tracker.DidNotReceive().GetConnectionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        caller.DidNotReceive().Abort();
    }

    // ---- helpers ----

    private void Allow() =>
        _rateLimiter
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new RateLimitResult(IsAllowed: true, CurrentCount: 1));

    private static ValueTask<object?> Next(HubInvocationContext _) => ValueTask.FromResult<object?>(null);

    private static HubCallerContext Caller(Guid? userId, string connectionId)
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.ConnectionAborted.Returns(CancellationToken.None);
        context.User.Returns(
            userId is null
                ? null
                : new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "Test")));
        return context;
    }

    private static HubInvocationContext Invocation(Guid? userId, string connectionId) =>
        new(
            Caller(userId, connectionId),
            Substitute.For<IServiceProvider>(),
            new TestHub(),
            typeof(TestHub).GetMethod(nameof(TestHub.Ping))!,
            []);

    private static HubLifetimeContext Lifetime(HubCallerContext caller) =>
        new(caller, Substitute.For<IServiceProvider>(), new TestHub());

    private sealed class TestHub : Hub
    {
        public Task Ping() => Task.CompletedTask;
    }
}
