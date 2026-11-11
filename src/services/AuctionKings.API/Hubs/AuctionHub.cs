using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Hubs;

[Authorize]
public sealed class AuctionHub(IConnectionTracker connectionTracker, IMediator mediator) : Hub
{
    private readonly IConnectionTracker _connectionTracker = connectionTracker;
    private readonly IMediator _mediator = mediator;

    public async Task JoinAuctionAsync(Guid auctionId)
    {
        // Resolve the auction first so clients cannot create groups for arbitrary/non-existent ids.
        var result = await _mediator.Send(new GetAuctionDetailsQuery(auctionId), Context.ConnectionAborted).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new HubException("Auction not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted).ConfigureAwait(false);
        await Clients.Caller.SendAsync("AuctionState", result.Value, Context.ConnectionAborted).ConfigureAwait(false);
    }

    public Task LeaveAuctionAsync(Guid auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted);

    public async Task RequestStateSyncAsync(Guid auctionId)
    {
        var result = await _mediator.Send(new GetAuctionDetailsQuery(auctionId), Context.ConnectionAborted).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("AuctionState", result.Value, Context.ConnectionAborted).ConfigureAwait(false);
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserId();
        if (userId.HasValue)
        {
            await _connectionTracker.TrackAsync(userId.Value, Context.ConnectionId).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.GetUserId();
        if (userId.HasValue)
        {
            await _connectionTracker.UntrackAsync(userId.Value, Context.ConnectionId).ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
