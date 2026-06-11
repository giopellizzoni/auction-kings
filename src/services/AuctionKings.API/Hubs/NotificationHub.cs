using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuctionKings.API.Hubs;

[Authorize]
public sealed class NotificationHub(IConnectionTracker connectionTracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId.Value}", Context.ConnectionAborted).ConfigureAwait(false);
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

        // No explicit Groups.RemoveFromGroupAsync call needed: the SignalR runtime
        // automatically removes the connection from all groups on disconnect.
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
