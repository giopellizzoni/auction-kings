using AuctionKings.Application.Abstractions;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Realtime;

/// <summary>
/// <see cref="IRealtimeNotificationService"/> for processes that do not host SignalR (the background worker).
/// It appends the notification to a Redis Stream; the API consumes the stream and pushes to the hubs.
/// Like the SignalR implementation it never throws: the operation already succeeded, only the push is lost.
/// </summary>
internal sealed partial class RedisStreamRealtimeNotificationService(
    IConnectionMultiplexer redis,
    IDateTimeProvider clock,
    ILogger<RedisStreamRealtimeNotificationService> logger) : IRealtimeNotificationService
{
    public Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default) =>
        PublishAsync(RealtimeKinds.BidPlaced, auctionId, new BidPlacedPayload(auctionId, bidId, amount, currency));

    public Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default) =>
        PublishAsync(RealtimeKinds.AuctionClosed, auctionId, new AuctionClosedPayload(auctionId, winnerId));

    public Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default) =>
        PublishAsync(RealtimeKinds.AuctionExtended, auctionId, new AuctionExtendedPayload(auctionId, newEndTime));

    public Task NotifyAuctionCancelledAsync(
        Guid sellerId,
        Guid auctionId,
        string title,
        DateTime cancelledAt,
        CancellationToken cancellationToken = default) =>
        PublishAsync(
            RealtimeKinds.AuctionCancelled,
            auctionId,
            new AuctionCancelledPayload(sellerId, auctionId, title, cancelledAt));

    private async Task PublishAsync<TPayload>(string kind, Guid auctionId, TPayload payload)
    {
        try
        {
            await redis
                .GetDatabase()
                .StreamAddAsync(
                    RealtimeFeed.Key,
                    RealtimeFeed.Field,
                    RealtimeFeed.Serialize(kind, clock.UtcNow, payload),
                    maxLength: RealtimeFeed.MaxLength,
                    useApproximateMaxLength: true)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogPublishFailed(logger, kind, auctionId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing real-time notification '{Kind}' for auction {AuctionId} to the stream failed. The operation succeeded; only the real-time push was lost.")]
    private static partial void LogPublishFailed(ILogger logger, string kind, Guid auctionId, Exception ex);
}

public static class RealtimeStreamServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stream-backed <see cref="IRealtimeNotificationService"/> for hosts without SignalR.
    /// Requires <c>AddInfrastructure</c> (for the Redis connection and the clock).
    /// </summary>
    public static IServiceCollection AddRealtimeStreamPublisher(this IServiceCollection services) =>
        services.AddSingleton<IRealtimeNotificationService, RedisStreamRealtimeNotificationService>();
}
