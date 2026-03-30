using AuctionKings.API.Hubs;
using AuctionKings.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Realtime;

public sealed class SignalRNotificationServiceTests
{
    private readonly IHubContext<AuctionHub> _hubContext = Substitute.For<IHubContext<AuctionHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.Group(Arg.Any<string>()).Returns(_groupProxy);
        _service = new SignalRNotificationService(_hubContext);
    }

    [Fact]
    public async Task SignalRNotificationService_BidPlaced_ShouldNotifyGroup()
    {
        var auctionId = Guid.NewGuid();

        await _service.NotifyBidPlacedAsync(auctionId, Guid.NewGuid(), Guid.NewGuid(), 150m, "USD");

        _clients.Received(1).Group($"auction-{auctionId}");
        await _groupProxy.Received(1).SendCoreAsync("BidPlaced", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignalRNotificationService_AuctionClosed_ShouldNotifyGroup()
    {
        var auctionId = Guid.NewGuid();

        await _service.NotifyAuctionClosedAsync(auctionId, null);

        _clients.Received(1).Group($"auction-{auctionId}");
        await _groupProxy.Received(1).SendCoreAsync("AuctionClosed", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignalRNotificationService_AuctionExtended_ShouldNotifyGroup()
    {
        var auctionId = Guid.NewGuid();
        var newEndTime = DateTime.UtcNow.AddHours(1);

        await _service.NotifyAuctionExtendedAsync(auctionId, newEndTime);

        _clients.Received(1).Group($"auction-{auctionId}");
        await _groupProxy.Received(1).SendCoreAsync("AuctionExtended", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
