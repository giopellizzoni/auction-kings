using AuctionKings.Application.Abstractions;

namespace AuctionKings.BackgroundWorkers;

/// <summary>
/// The real SignalR-backed implementation (<c>SignalRNotificationService</c>) lives in
/// AuctionKings.API, not Infrastructure, specifically to avoid a circular project reference —
/// the worker doesn't (and shouldn't) reference the API project, so it has no IHubContext to
/// push through. CloseAuctionCommandHandler still depends on IRealtimeNotificationService
/// (it's dispatched from here by AuctionCloseWorker), so something must be registered or the
/// DI container fails to build. Until cross-process SignalR delivery (e.g. an IHubContext
/// wired to the same Redis backplane the API uses) is implemented for the worker, auction
/// close/extension events raised from this process are not pushed to connected clients.
/// </summary>
internal sealed class NoOpRealtimeNotificationService : IRealtimeNotificationService
{
    public Task NotifyBidPlacedAsync(
        Guid auctionId,
        Guid bidId,
        Guid bidderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyAuctionClosedAsync(
        Guid auctionId,
        Guid? winnerId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyAuctionExtendedAsync(
        Guid auctionId,
        DateTime newEndTime,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
