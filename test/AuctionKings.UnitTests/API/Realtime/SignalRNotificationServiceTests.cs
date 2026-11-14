using AuctionKings.API.Hubs;
using AuctionKings.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Realtime;

public sealed class SignalRNotificationServiceTests
{
    private readonly IHubContext<AuctionHub> _hubContext = Substitute.For<IHubContext<AuctionHub>>();
    private readonly IHubContext<NotificationHub> _notificationHubContext = Substitute.For<IHubContext<NotificationHub>>();
    private readonly IHubClients _notificationClients = Substitute.For<IHubClients>();
    private readonly IClientProxy _userProxy = Substitute.For<IClientProxy>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.Group(Arg.Any<string>()).Returns(_groupProxy);
        _notificationHubContext.Clients.Returns(_notificationClients);
        _notificationClients.Group(Arg.Any<string>()).Returns(_userProxy);
        _service = new SignalRNotificationService(_hubContext, _notificationHubContext, NullLogger<SignalRNotificationService>.Instance);
    }

    [Fact]
    public async Task SignalRNotificationService_BidPlaced_ShouldNotifyGroup()
    {
        var auctionId = Guid.NewGuid();

        await _service.NotifyBidPlacedAsync(auctionId, Guid.NewGuid(), 150m, "USD");

        _clients.Received(1).Group($"auction-{auctionId}");
        await _groupProxy.Received(1).SendCoreAsync("BidPlaced", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BidPlacedNotificationDto_ShouldNotExposeBidderIdentity()
    {
        // The event is broadcast to every watcher of the auction; bidding is never public.
        Assert.DoesNotContain(
            "BidderId",
            typeof(AuctionKings.Application.DTOs.BidPlacedNotificationDto).GetProperties().Select(p => p.Name));
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
    public async Task SignalRNotificationService_AuctionCancelled_ShouldNotifySellerGroupOnly()
    {
        var sellerId = Guid.NewGuid();

        await _service.NotifyAuctionCancelledAsync(sellerId, Guid.NewGuid(), "Vase", DateTime.UtcNow);

        _notificationClients.Received(1).Group($"user-{sellerId}");
        await _userProxy.Received(1).SendCoreAsync("AuctionCancelled", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        _clients.DidNotReceive().Group(Arg.Any<string>());
    }

    [Fact]
    public void AuctionCancelledNotificationDto_ShouldNotExposeBidderInformation()
    {
        var names = typeof(AuctionKings.Application.DTOs.AuctionCancelledNotificationDto)
            .GetProperties().Select(p => p.Name);

        Assert.DoesNotContain(
            names,
            n => n.Contains("Bid", StringComparison.Ordinal) || n.Contains("Winner", StringComparison.Ordinal));
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
