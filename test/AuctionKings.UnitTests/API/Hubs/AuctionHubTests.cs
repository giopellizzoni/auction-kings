using System.Security.Claims;
using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Hubs;

public sealed class AuctionHubTests : IDisposable
{
    private readonly IConnectionTracker _connectionTracker = Substitute.For<IConnectionTracker>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly AuctionHub _hub;

    public AuctionHubTests()
    {
        _context.ConnectionId.Returns("conn-1");
        _hub = new AuctionHub(_connectionTracker)
        {
            Groups = _groups,
            Context = _context,
        };
    }

    [Fact]
    public async Task AuctionHub_JoinAuction_ShouldAddToGroup()
    {
        var auctionId = Guid.NewGuid();

        await _hub.JoinAuctionAsync(auctionId);

        await _groups.Received(1).AddToGroupAsync("conn-1", $"auction-{auctionId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuctionHub_LeaveAuction_ShouldRemoveFromGroup()
    {
        var auctionId = Guid.NewGuid();

        await _hub.LeaveAuctionAsync(auctionId);

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", $"auction-{auctionId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuctionHub_OnConnectedAsync_AuthenticatedUser_ShouldTrackConnection()
    {
        var userId = Guid.NewGuid();
        _context.User.Returns(BuildPrincipal(userId));

        await _hub.OnConnectedAsync();

        await _connectionTracker.Received(1).TrackAsync(userId, "conn-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuctionHub_OnConnectedAsync_AnonymousUser_ShouldNotTrack()
    {
        _context.User.Returns((ClaimsPrincipal?)null);

        await _hub.OnConnectedAsync();

        await _connectionTracker.DidNotReceive().TrackAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuctionHub_OnDisconnectedAsync_ShouldUntrackConnection()
    {
        var userId = Guid.NewGuid();
        _context.User.Returns(BuildPrincipal(userId));

        await _hub.OnDisconnectedAsync(null);

        await _connectionTracker.Received(1).UntrackAsync(userId, "conn-1", Arg.Any<CancellationToken>());
    }

    public void Dispose() => _hub.Dispose();

    private static ClaimsPrincipal BuildPrincipal(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));
}
