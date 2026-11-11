using System.Security.Claims;
using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Enums;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Hubs;

public sealed class AuctionHubTests : IDisposable
{
    private const int MaxGroups = 2;

    private readonly IConnectionTracker _connectionTracker = Substitute.For<IConnectionTracker>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly ISingleClientProxy _caller = Substitute.For<ISingleClientProxy>();
    private readonly AuctionHub _hub;

    public AuctionHubTests()
    {
        _context.ConnectionId.Returns("conn-1");
        _context.Items.Returns(new Dictionary<object, object?>());
        _clients.Caller.Returns(_caller);
        _hub = new AuctionHub(_connectionTracker, _mediator, Options.Create(new HubThrottlingOptions { MaxGroupsPerConnection = MaxGroups }))
        {
            Groups = _groups,
            Context = _context,
            Clients = _clients,
        };
    }

    [Fact]
    public async Task AuctionHub_JoinAuction_ShouldAddToGroup()
    {
        var auctionId = Guid.NewGuid();
        var dto = BuildAuctionDetailDto(auctionId);
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

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

    [Fact]
    public async Task JoinAuctionAsync_ShouldSendAuctionStateToCaller_AfterJoiningGroup()
    {
        var auctionId = Guid.NewGuid();
        var dto = BuildAuctionDetailDto(auctionId);
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _hub.JoinAuctionAsync(auctionId);

        await _caller.Received(1).SendCoreAsync(
            "AuctionState",
            Arg.Is<object[]>(args => args.Length == 1 && args[0].Equals(dto)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinAuctionAsync_WhenAuctionNotFound_ShouldThrow_AndNotJoinGroupOrSendState()
    {
        var auctionId = Guid.NewGuid();
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuctionDetailDto>(Error.NotFound("Auction.NotFound", "Auction not found")));

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.JoinAuctionAsync(auctionId));

        Assert.Equal("Auction not found", ex.Message);
        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _caller.DidNotReceive().SendCoreAsync(
            "AuctionState",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinAuctionAsync_WhenAtGroupCap_ShouldThrow_WithoutQueryingOrJoining()
    {
        await JoinOkAsync(Guid.NewGuid());
        await JoinOkAsync(Guid.NewGuid());
        _mediator.ClearReceivedCalls();
        _groups.ClearReceivedCalls();

        await Assert.ThrowsAsync<HubException>(() => _hub.JoinAuctionAsync(Guid.NewGuid()));

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(GetAuctionDetailsQuery)!, default);
        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinAuctionAsync_RejoiningSameAuction_ShouldNotUseAnotherSlot()
    {
        var first = Guid.NewGuid();
        await JoinOkAsync(first);
        await JoinOkAsync(first);
        await JoinOkAsync(first);

        await JoinOkAsync(Guid.NewGuid()); // the second distinct auction still fits under the cap of 2

        await Assert.ThrowsAsync<HubException>(() => _hub.JoinAuctionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task LeaveAuctionAsync_ShouldFreeAGroupSlot()
    {
        var first = Guid.NewGuid();
        await JoinOkAsync(first);
        await JoinOkAsync(Guid.NewGuid());

        await _hub.LeaveAuctionAsync(first);

        await JoinOkAsync(Guid.NewGuid()); // would have thrown at the cap before leaving
    }

    [Fact]
    public async Task JoinAuctionAsync_FailedJoin_ShouldNotConsumeASlot()
    {
        var missing = Guid.NewGuid();
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == missing), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuctionDetailDto>(Error.NotFound("Auction.NotFound", "Auction not found")));
        for (var i = 0; i < MaxGroups + 3; i++)
        {
            await Assert.ThrowsAsync<HubException>(() => _hub.JoinAuctionAsync(missing));
        }

        await JoinOkAsync(Guid.NewGuid());
    }

    private async Task JoinOkAsync(Guid auctionId)
    {
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(BuildAuctionDetailDto(auctionId)));

        await _hub.JoinAuctionAsync(auctionId);
    }

    [Fact]
    public async Task RequestStateSyncAsync_ShouldSendAuctionStateToCaller()
    {
        var auctionId = Guid.NewGuid();
        var dto = BuildAuctionDetailDto(auctionId);
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _hub.RequestStateSyncAsync(auctionId);

        await _caller.Received(1).SendCoreAsync(
            "AuctionState",
            Arg.Is<object[]>(args => args.Length == 1 && args[0].Equals(dto)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestStateSyncAsync_WhenAuctionNotFound_ShouldNotSendState()
    {
        var auctionId = Guid.NewGuid();
        _mediator
            .Send(Arg.Is<GetAuctionDetailsQuery>(q => q.AuctionId == auctionId), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuctionDetailDto>(Error.NotFound("Auction.NotFound", "Auction not found")));

        await _hub.RequestStateSyncAsync(auctionId);

        await _caller.DidNotReceive().SendCoreAsync(
            "AuctionState",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _hub.Dispose();

    private static ClaimsPrincipal BuildPrincipal(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));

    private static AuctionDetailDto BuildAuctionDetailDto(Guid auctionId) =>
        new(
            auctionId,
            "Test Auction",
            "A test description",
            100m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            AuctionStatus.Active,
            0);
}
