using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Realtime;

internal sealed class SignalRNotificationService(IHubContext<AuctionHub> auctionHub) : IRealtimeNotificationService
{
    public Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        Guid bidderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default) =>
        auctionHub.Clients
            .Group($"auction-{auctionId}")
            .SendAsync(
                "BidPlaced",
                new { AuctionId = auctionId, BidId = bidId, BidderId = bidderId, Amount = amount, Currency = currency },
                cancellationToken);

    public Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default) =>
        auctionHub.Clients
            .Group($"auction-{auctionId}")
            .SendAsync(
                "AuctionClosed",
                new { AuctionId = auctionId, WinnerId = winnerId },
                cancellationToken);

    public Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default) =>
        auctionHub.Clients
            .Group($"auction-{auctionId}")
            .SendAsync(
                "AuctionExtended",
                new { AuctionId = auctionId, NewEndTime = newEndTime },
                cancellationToken);
}
