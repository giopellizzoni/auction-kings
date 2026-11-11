using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Hubs;

[Authorize]
public sealed class AuctionHub(
    IConnectionTracker connectionTracker,
    IMediator mediator,
    IOptions<HubThrottlingOptions> throttlingOptions) : Hub
{
    private const string JoinedAuctionsKey = "joined-auctions";

    private readonly IConnectionTracker _connectionTracker = connectionTracker;
    private readonly IMediator _mediator = mediator;
    private readonly int _maxGroupsPerConnection = throttlingOptions.Value.MaxGroupsPerConnection;

    // Per-connection state lives in Context.Items. It is not synchronised because SignalR runs one
    // invocation at a time per connection (MaximumParallelInvocationsPerClient = 1, see AddHubThrottling).
    private HashSet<Guid> JoinedAuctions
    {
        get
        {
            if (Context.Items.TryGetValue(JoinedAuctionsKey, out var existing) && existing is HashSet<Guid> joined)
            {
                return joined;
            }

            var created = new HashSet<Guid>();
            Context.Items[JoinedAuctionsKey] = created;
            return created;
        }
    }

    public async Task JoinAuctionAsync(Guid auctionId)
    {
        var joined = JoinedAuctions;

        // Cheap check first. Rejoining an auction already joined does not use another slot.
        if (!joined.Contains(auctionId) && joined.Count >= _maxGroupsPerConnection)
        {
            throw new HubException($"You can follow at most {_maxGroupsPerConnection} auctions at once. Leave one first.");
        }

        // Validate before touching the group: joining first let a connection collect an unbounded
        // number of groups by joining random GUIDs.
        var result = await _mediator.Send(new GetAuctionDetailsQuery(auctionId), Context.ConnectionAborted).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new HubException(result.Error.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted).ConfigureAwait(false);
        joined.Add(auctionId);

        await Clients.Caller.SendAsync("AuctionState", result.Value, Context.ConnectionAborted).ConfigureAwait(false);
    }

    public async Task LeaveAuctionAsync(Guid auctionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction-{auctionId}", Context.ConnectionAborted).ConfigureAwait(false);
        JoinedAuctions.Remove(auctionId);
    }

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
