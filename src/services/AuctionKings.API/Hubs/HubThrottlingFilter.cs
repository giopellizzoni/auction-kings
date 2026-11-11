using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Hubs;

/// <summary>
/// Global hub filter that throttles every hub: it rate-limits method invocations per user and
/// caps concurrent connections per user. Being a filter, it covers every current and future hub
/// method without per-method code.
/// </summary>
internal sealed partial class HubThrottlingFilter(
    IRateLimiter rateLimiter,
    IConnectionTracker connectionTracker,
    IOptions<HubThrottlingOptions> options,
    ILogger<HubThrottlingFilter> logger) : IHubFilter
{
    private readonly HubThrottlingOptions _options = options.Value;

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var caller = invocationContext.Context;

        // Keyed by user, not connection, so opening more connections does not buy more calls.
        var identity = caller.GetUserId()?.ToString() ?? caller.ConnectionId;

        var result = await rateLimiter
            .IsAllowedAsync($"rate:hub:{identity}", _options.MaxInvocations, _options.Window, caller.ConnectionAborted)
            .ConfigureAwait(false);

        // On a Redis error the limiter has already logged and reports IsAllowed = true (fail open):
        // hub methods only read cached data, and if Redis is down the SignalR backplane is too.
        if (!result.IsAllowed)
        {
            LogInvocationThrottled(logger, invocationContext.HubMethodName, identity);
            throw new HubException("Rate limit exceeded. Slow down and retry shortly.");
        }

        return await next(invocationContext).ConfigureAwait(false);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        // The hub's own OnConnectedAsync registers the connection with the tracker, so run it first
        // and count afterwards.
        await next(context).ConfigureAwait(false);

        var userId = context.Context.GetUserId();
        if (userId is null)
        {
            return;
        }

        var connections = await connectionTracker
            .GetConnectionsAsync(userId.Value, context.Context.ConnectionAborted)
            .ConfigureAwait(false);

        // Not atomic: simultaneous connects can briefly exceed the cap (see issue #127).
        if (connections.Count > _options.MaxConnectionsPerUser)
        {
            LogConnectionRejected(logger, userId.Value, connections.Count, _options.MaxConnectionsPerUser);
            context.Context.Abort();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hub method {Method} throttled for {Identity}.")]
    private static partial void LogInvocationThrottled(ILogger logger, string method, string identity);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Rejected hub connection for user {UserId}: {Count} connections exceeds the limit of {Limit}.")]
    private static partial void LogConnectionRejected(ILogger logger, Guid userId, int count, int limit);
}
