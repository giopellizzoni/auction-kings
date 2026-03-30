using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Hubs;

// TODO: Add [Authorize] once authentication (Cognito/JWT) is implemented — Task 10
public sealed class AuctionHub(IConnectionTracker connectionTracker) : Hub
{
    public Task JoinAuctionAsync(Guid auctionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted);

    public Task LeaveAuctionAsync(Guid auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted);

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserId();
        if (userId.HasValue)
        {
            await connectionTracker.TrackAsync(userId.Value, Context.ConnectionId).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.GetUserId();
        if (userId.HasValue)
        {
            await connectionTracker.UntrackAsync(userId.Value, Context.ConnectionId).ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
