using System.Security.Claims;
using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Hubs;

public sealed class NotificationHubTests : IDisposable
{
    private readonly IConnectionTracker _connectionTracker = Substitute.For<IConnectionTracker>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly NotificationHub _hub;

    public NotificationHubTests()
    {
        _context.ConnectionId.Returns("conn-1");
        _hub = new NotificationHub(_connectionTracker)
        {
            Groups = _groups,
            Context = _context,
        };
    }

    [Fact]
    public async Task NotificationHub_OnConnectedAsync_AuthenticatedUser_ShouldJoinUserGroup()
    {
        var userId = Guid.NewGuid();
        _context.User.Returns(BuildPrincipal(userId));

        await _hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync("conn-1", $"user-{userId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationHub_OnConnectedAsync_AuthenticatedUser_ShouldTrackConnection()
    {
        var userId = Guid.NewGuid();
        _context.User.Returns(BuildPrincipal(userId));

        await _hub.OnConnectedAsync();

        await _connectionTracker.Received(1).TrackAsync(userId, "conn-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationHub_OnConnectedAsync_AnonymousUser_ShouldNotJoinGroupOrTrack()
    {
        _context.User.Returns((ClaimsPrincipal?)null);

        await _hub.OnConnectedAsync();

        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _connectionTracker.DidNotReceive().TrackAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationHub_OnDisconnectedAsync_ShouldUntrackConnection()
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
