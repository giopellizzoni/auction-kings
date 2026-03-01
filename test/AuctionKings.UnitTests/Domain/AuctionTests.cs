using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;

namespace AuctionKings.UnitTests.Domain;

public sealed class AuctionTests
{
    private static readonly Money StartingPrice = new(100m, "USD");
    private static readonly AuctionDuration Duration = AuctionDuration.FromDays(1);
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Auction CreateActiveAuction(
        Money? startingPrice = null,
        DateTime? startTime = null,
        AuctionDuration? duration = null,
        Guid? sellerId = null
    )
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            startingPrice ?? StartingPrice,
            startTime ?? Now,
            duration ?? Duration,
            sellerId ?? SellerId
        );

        auction.Activate(Now);
        auction.ClearDomainEvents();
        return auction;
    }

    [Fact]
    public void Auction_Create_ShouldRaiseAuctionCreatedEvent()
    {
        var auction = Auction.Create("Test", "Desc", StartingPrice, Now, Duration, SellerId);

        var events = auction.GetDomainEvents();
        Assert.Single(events);
        Assert.IsType<AuctionCreatedEvent>(events.First());
    }

    [Fact]
    public void Auction_PlaceBid_ValidBid_ShouldSucceed()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        var bidAmount = new Money(110m, "USD"); // minimum is 100 + 5 = 105

        var result = auction.PlaceBid(bidderId, bidAmount, Now);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(bidAmount, auction.CurrentPrice);
    }

    [Fact]
    public void Auction_PlaceBid_BelowMinimumIncrement_ShouldFail()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        var bidAmount = new Money(101m, "USD"); // minimum is 100 + 5 = 105

        var result = auction.PlaceBid(bidderId, bidAmount, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.BidTooLow", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_OnClosedAuction_ShouldFail()
    {
        var auction = CreateActiveAuction();
        auction.Close(Now);
        auction.ClearDomainEvents();

        var result = auction.PlaceBid(Guid.NewGuid(), new Money(200m, "USD"), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.NotActive", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_BySeller_ShouldFail()
    {
        var auction = CreateActiveAuction(sellerId: SellerId);

        var result = auction.PlaceBid(SellerId, new Money(110m, "USD"), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.CannotBidOnOwnAuction", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_NearEnd_ShouldExtendAuction()
    {
        var startTime = Now.AddMinutes(-60);
        var shortDuration = new AuctionDuration(TimeSpan.FromMinutes(62)); // ends in 2 min
        var auction = CreateActiveAuction(startTime: startTime, duration: shortDuration);
        var originalEndTime = auction.EndTime;

        var bidTime = startTime.AddMinutes(60); // 2 min before end
        auction.PlaceBid(Guid.NewGuid(), new Money(110m, "USD"), bidTime);

        Assert.True(auction.EndTime > originalEndTime);
        var events = auction.GetDomainEvents();
        Assert.Contains(events, e => e is AuctionExtendedEvent);
    }

    [Fact]
    public void Auction_PlaceBid_MarksOldWinningBidAsOutbid()
    {
        var auction = CreateActiveAuction();
        var bidder1 = Guid.NewGuid();
        var bidder2 = Guid.NewGuid();

        var firstBidResult = auction.PlaceBid(bidder1, new Money(110m, "USD"), Now);
        var firstBid = firstBidResult.Value;

        auction.PlaceBid(bidder2, new Money(130m, "USD"), Now);

        Assert.Equal(BidStatus.Outbid, firstBid.Status);
    }

    [Fact]
    public void Auction_Close_WithBids_ShouldDetermineWinner()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        auction.PlaceBid(bidderId, new Money(110m, "USD"), Now);
        auction.ClearDomainEvents();

        auction.Close(Now);

        Assert.Equal(AuctionStatus.Closed, auction.Status);
        Assert.Equal(bidderId, auction.WinnerId);

        var events = auction.GetDomainEvents();
        Assert.Contains(events, e => e is AuctionClosedEvent);
        Assert.Contains(events, e => e is WinnerDeterminedEvent);
    }

    [Fact]
    public void Auction_Close_NoBids_ShouldCloseWithoutWinner()
    {
        var auction = CreateActiveAuction();
        auction.Close(Now);

        Assert.Equal(AuctionStatus.Closed, auction.Status);
        Assert.Null(auction.WinnerId);

        var events = auction.GetDomainEvents();
        var closedEvent = Assert.Single(events.OfType<AuctionClosedEvent>());
        Assert.Null(closedEvent.WinnerId);
    }

    [Fact]
    public void Auction_Close_AlreadyClosed_ShouldFail()
    {
        var auction = CreateActiveAuction();
        auction.Close(Now);
        auction.ClearDomainEvents();

        var result = auction.Close(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.AlreadyClosed", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_RaisesBidPlacedEvent()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();

        auction.PlaceBid(bidderId, new Money(110m, "USD"), Now);

        var events = auction.GetDomainEvents();
        Assert.Contains(events, e => e is BidPlacedEvent);
    }
}
