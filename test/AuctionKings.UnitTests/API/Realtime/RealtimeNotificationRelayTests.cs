using AuctionKings.API.Realtime;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Realtime;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Realtime;

public sealed class RealtimeNotificationRelayTests
{
    private readonly IRealtimeNotificationService _target = Substitute.For<IRealtimeNotificationService>();

    [Fact]
    public async Task DispatchAsync_AuctionClosed_ShouldNotifyClosed()
    {
        var auctionId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        await RealtimeNotificationRelay.DispatchAsync(
            new RealtimeNotification(RealtimeNotificationType.AuctionClosed, auctionId, WinnerId: winnerId),
            _target,
            CancellationToken.None);

        await _target.Received(1).NotifyAuctionClosedAsync(auctionId, winnerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_AuctionExtended_ShouldNotifyExtended()
    {
        var auctionId = Guid.NewGuid();
        var newEnd = new DateTime(2026, 1, 1, 12, 5, 0, DateTimeKind.Utc);

        await RealtimeNotificationRelay.DispatchAsync(
            new RealtimeNotification(RealtimeNotificationType.AuctionExtended, auctionId, NewEndTime: newEnd),
            _target,
            CancellationToken.None);

        await _target.Received(1).NotifyAuctionExtendedAsync(auctionId, newEnd, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_BidPlaced_ShouldNotifyBidPlaced()
    {
        var auctionId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var bidderId = Guid.NewGuid();

        await RealtimeNotificationRelay.DispatchAsync(
            new RealtimeNotification(
                RealtimeNotificationType.BidPlaced, auctionId,
                BidId: bidId, BidderId: bidderId, Amount: 110m, Currency: "USD"),
            _target,
            CancellationToken.None);

        await _target.Received(1).NotifyBidPlacedAsync(auctionId, bidId, bidderId, 110m, "USD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_IncompleteNotification_ShouldThrow()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RealtimeNotificationRelay.DispatchAsync(
            new RealtimeNotification(RealtimeNotificationType.AuctionExtended, Guid.NewGuid()),
            _target,
            CancellationToken.None));
    }

    [Fact]
    public void Envelope_ShouldRoundTripThroughStreamSerialization()
    {
        var original = new RealtimeNotification(
            RealtimeNotificationType.AuctionClosed, Guid.NewGuid(), WinnerId: Guid.NewGuid());

        var roundTripped = RealtimeNotificationChannel.Deserialize(RealtimeNotificationChannel.Serialize(original));

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void NextIdleDelay_ShouldDoubleUpToCap()
    {
        var delay = TimeSpan.FromMilliseconds(250);
        var observed = new List<TimeSpan>();
        for (var i = 0; i < 6; i++)
        {
            delay = RealtimeNotificationRelay.NextIdleDelay(delay);
            observed.Add(delay);
        }

        Assert.Equal(TimeSpan.FromMilliseconds(500), observed[0]);
        Assert.Equal(TimeSpan.FromSeconds(1), observed[1]);
        Assert.All(observed.Skip(2), d => Assert.Equal(TimeSpan.FromSeconds(2), d));
    }

    [Theory]
    [InlineData("other", 0, 11 * 60 * 1000, true)]   // idle, nothing pending → dead
    [InlineData("other", 3, 11 * 60 * 1000, false)]  // still owns pending entries → keep (XAUTOCLAIM first)
    [InlineData("other", 0, 30 * 1000, false)]       // recently active → alive
    [InlineData("self", 0, 11 * 60 * 1000, false)]   // never delete ourselves
    public void IsDeadConsumer_ShouldOnlyMatchIdleConsumersWithNothingPending(
        string name, long pending, long idleMs, bool expected)
    {
        Assert.Equal(expected, RealtimeNotificationRelay.IsDeadConsumer(name, pending, idleMs, "self"));
    }
}
