using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Realtime;

internal sealed partial class SignalRNotificationService(
    IHubContext<AuctionHub> auctionHub,
    ILogger<SignalRNotificationService> logger) : IRealtimeNotificationService
{
    public async Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auctionHub.Clients
                .Group($"auction-{auctionId}")
                .SendAsync(
                    "BidPlaced",
                    new BidPlacedNotificationDto(auctionId, bidId, amount, currency),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            LogNotifyFailed(logger, nameof(NotifyBidPlacedAsync), auctionId, ex);
        }
    }

    public async Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auctionHub.Clients
                .Group($"auction-{auctionId}")
                .SendAsync(
                    "AuctionClosed",
                    new AuctionClosedNotificationDto(auctionId, winnerId),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            LogNotifyFailed(logger, nameof(NotifyAuctionClosedAsync), auctionId, ex);
        }
    }

    public async Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auctionHub.Clients
                .Group($"auction-{auctionId}")
                .SendAsync(
                    "AuctionExtended",
                    new AuctionExtendedNotificationDto(auctionId, newEndTime),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            LogNotifyFailed(logger, nameof(NotifyAuctionExtendedAsync), auctionId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SignalR notification '{Method}' failed for auction {AuctionId}. The operation succeeded; only the real-time push was lost.")]
    private static partial void LogNotifyFailed(ILogger logger, string method, Guid auctionId, Exception ex);
}
