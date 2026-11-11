using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Realtime;

/// <summary>
/// <see cref="IRealtimeNotificationService"/> for processes without SignalR (the background worker).
/// Notifications are appended to <see cref="RealtimeNotificationChannel.Key"/> and relayed to clients
/// by the API. Like the SignalR implementation, failures are logged and swallowed: the state change
/// has already been committed and only the push is lost (clients can resync via RequestStateSync).
/// </summary>
internal sealed partial class RedisStreamRealtimeNotificationPublisher(
    IConnectionMultiplexer redis,
    ILogger<RedisStreamRealtimeNotificationPublisher> logger) : IRealtimeNotificationService
{
    public Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        Guid bidderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default) =>
        PublishAsync(new RealtimeNotification(
            RealtimeNotificationType.BidPlaced,
            auctionId,
            BidId: bidId,
            BidderId: bidderId,
            Amount: amount,
            Currency: currency));

    public Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default) =>
        PublishAsync(new RealtimeNotification(
            RealtimeNotificationType.AuctionClosed,
            auctionId,
            WinnerId: winnerId));

    public Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default) =>
        PublishAsync(new RealtimeNotification(
            RealtimeNotificationType.AuctionExtended,
            auctionId,
            NewEndTime: newEndTime));

    private async Task PublishAsync(RealtimeNotification notification)
    {
        try
        {
            await redis.GetDatabase().StreamAddAsync(
                RealtimeNotificationChannel.Key,
                RealtimeNotificationChannel.PayloadField,
                RealtimeNotificationChannel.Serialize(notification),
                maxLength: RealtimeNotificationChannel.MaxLength,
                useApproximateMaxLength: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogPublishFailed(logger, notification.Type, notification.AuctionId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enqueue real-time notification '{Type}' for auction {AuctionId}. The operation succeeded; only the push was lost.")]
    private static partial void LogPublishFailed(ILogger logger, string type, Guid auctionId, Exception ex);
}
