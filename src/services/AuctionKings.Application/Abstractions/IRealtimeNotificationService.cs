namespace AuctionKings.Application.Abstractions;

public interface IRealtimeNotificationService
{
    Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default
    );

    Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default
    );

    Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default
    );
}
