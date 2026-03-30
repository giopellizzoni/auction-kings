using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
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
                new BidPlacedNotificationDto(auctionId, bidId, bidderId, amount, currency),
                cancellationToken);

    public Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default) =>
        auctionHub.Clients
            .Group($"auction-{auctionId}")
            .SendAsync(
                "AuctionClosed",
                new AuctionClosedNotificationDto(auctionId, winnerId),
                cancellationToken);

    public Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default) =>
        auctionHub.Clients
            .Group($"auction-{auctionId}")
            .SendAsync(
                "AuctionExtended",
                new AuctionExtendedNotificationDto(auctionId, newEndTime),
                cancellationToken);
}
