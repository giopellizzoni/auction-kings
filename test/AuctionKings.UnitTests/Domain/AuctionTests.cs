using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

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
        ).Value;

        auction.Activate(Now);
        auction.ClearDomainEvents();
        return auction;
    }

    private static Auction CreatePendingAuction(DateTime futureStart)
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            StartingPrice,
            futureStart,
            Duration,
            SellerId
        ).Value;
        auction.ClearDomainEvents();
        return auction;
    }

    [Fact]
    public void Auction_Create_ShouldRaiseAuctionCreatedEvent()
    {
        var auction = Auction.Create("Test", "Desc", StartingPrice, Now, Duration, SellerId).Value;

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

    [Fact]
    public void Auction_PlaceBid_AfterEndTime_ShouldFail()
    {
        var auction = CreateActiveAuction();
        var bidTime = auction.EndTime; // exactly at end is past

        var result = auction.PlaceBid(Guid.NewGuid(), new Money(110m, "USD"), bidTime);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.Ended", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_AfterEndTime_ShouldNotExtendAuction()
    {
        var auction = CreateActiveAuction();
        var endTime = auction.EndTime;

        auction.PlaceBid(Guid.NewGuid(), new Money(110m, "USD"), endTime.AddSeconds(1));

        Assert.Equal(endTime, auction.EndTime);
    }

    [Fact]
    public void Auction_PlaceBid_SelfOutbid_ShouldFail()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();

        auction.PlaceBid(bidderId, new Money(110m, "USD"), Now);
        var result = auction.PlaceBid(bidderId, new Money(130m, "USD"), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.CannotOutbidSelf", result.Error.Code);
    }

    [Fact]
    public void Auction_PlaceBid_CurrencyMismatch_ShouldFail()
    {
        var auction = CreateActiveAuction(startingPrice: new Money(100m, "USD"));

        var result = auction.PlaceBid(Guid.NewGuid(), new Money(110m, "EUR"), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Bid.CurrencyMismatch", result.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Auction_Create_WithBlankTitle_ShouldFailWithTitleRequired(string? title)
    {
        var result = Auction.Create(title!, "Desc", StartingPrice, Now, Duration, SellerId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.TitleRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Auction_Create_WithEmptySeller_ShouldFailWithSellerRequired()
    {
        var result = Auction.Create("Test", "Desc", StartingPrice, Now, Duration, Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.SellerRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Auction_Create_WithBlankTitleAndEmptySeller_ShouldReportTheTitleFirst()
    {
        var result = Auction.Create(" ", "Desc", StartingPrice, Now, Duration, Guid.Empty);

        Assert.Equal(DomainErrors.Auction.TitleRequired, result.Error);
    }

    [Fact]
    public void Auction_Create_WhenItFails_ShouldNotThrow_AndGiveNoAuction()
    {
        var result = Auction.Create("", "Desc", StartingPrice, Now, Duration, SellerId);

        // A failed Result carries an error, never a value: reading it is a programming error.
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Auction_Create_ShouldReturnTheAuctionWithTheGivenValues()
    {
        var result = Auction.Create("Vintage Watch", null!, StartingPrice, Now, Duration, SellerId);

        Assert.True(result.IsSuccess);
        var auction = result.Value;
        Assert.Equal("Vintage Watch", auction.Title);
        Assert.Equal(string.Empty, auction.Description); // a null description is normalised, not rejected
        Assert.Equal(SellerId, auction.SellerId);
        Assert.Equal(StartingPrice, auction.StartingPrice);
        Assert.Equal(StartingPrice, auction.CurrentPrice);
        Assert.Equal(Now.Add(Duration.Value), auction.EndTime);
    }

    [Fact]
    public void Auction_Create_WithPastStartTime_ShouldBeActive()
    {
        var pastStart = DateTime.UtcNow.AddHours(-1);

        var auction = Auction.Create("Test", "Desc", StartingPrice, pastStart, Duration, SellerId).Value;

        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Auction_Create_WithFutureStartTime_ShouldBePending()
    {
        var futureStart = DateTime.UtcNow.AddHours(2);

        var auction = Auction.Create("Test", "Desc", StartingPrice, futureStart, Duration, SellerId).Value;

        Assert.Equal(AuctionStatus.Pending, auction.Status);
    }

    [Fact]
    public void Auction_Activate_PendingWhenStartTimeReached_ShouldSucceedAndRaiseEvent()
    {
        var futureStart = DateTime.UtcNow.AddHours(2);
        var auction = CreatePendingAuction(futureStart);

        var result = auction.Activate(futureStart.AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Active, auction.Status);
        Assert.Contains(auction.GetDomainEvents(), e => e is AuctionActivatedEvent);
    }

    [Fact]
    public void Auction_Activate_BeforeStartTime_ShouldBeNoOp()
    {
        var futureStart = DateTime.UtcNow.AddHours(2);
        var auction = CreatePendingAuction(futureStart);

        var result = auction.Activate(futureStart.AddMinutes(-30));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Pending, auction.Status);
        Assert.Empty(auction.GetDomainEvents());
    }

    [Fact]
    public void Auction_Activate_PendingAfterEndTime_ShouldFailWithEndedAndChangeNothing()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var auction = CreatePendingAuction(start);
        var version = auction.Version;

        var result = auction.Activate(auction.EndTime.AddSeconds(1));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.Ended, result.Error);
        Assert.Equal(AuctionStatus.Pending, auction.Status);
        Assert.Empty(auction.GetDomainEvents());
        Assert.Equal(version, auction.Version);
    }

    [Fact]
    public void Auction_Activate_PendingExactlyAtEndTime_ShouldFailWithEnded()
    {
        var auction = CreatePendingAuction(DateTime.UtcNow.AddHours(2));
        var version = auction.Version;

        var result = auction.Activate(auction.EndTime);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.Ended, result.Error);
        Assert.Equal(AuctionStatus.Pending, auction.Status);
        Assert.Empty(auction.GetDomainEvents());
        Assert.Equal(version, auction.Version);
    }

    [Fact]
    public void Auction_Activate_PendingJustBeforeEndTime_ShouldSucceedAndIncrementVersion()
    {
        var auction = CreatePendingAuction(DateTime.UtcNow.AddHours(2));
        var version = auction.Version;

        var result = auction.Activate(auction.EndTime.AddTicks(-1));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Active, auction.Status);
        Assert.Equal(version + 1, auction.Version);
        Assert.Single(auction.GetDomainEvents(), e => e is AuctionActivatedEvent);
    }

    [Fact]
    public void Auction_Activate_AlreadyActiveAndPastEndTime_ShouldStayIdempotentSuccess()
    {
        var auction = CreateActiveAuction();
        var version = auction.Version;

        var result = auction.Activate(auction.EndTime.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Active, auction.Status);
        Assert.Equal(version, auction.Version);
        Assert.Empty(auction.GetDomainEvents());
    }

    [Fact]
    public void Auction_Activate_AlreadyActive_ShouldBeIdempotent()
    {
        var auction = CreateActiveAuction();

        var result = auction.Activate(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Auction_Cancel_AlreadyCancelled_ShouldFail()
    {
        var auction = CreateActiveAuction();
        auction.Cancel(Now);
        auction.ClearDomainEvents();

        var result = auction.Cancel(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.AlreadyCancelled", result.Error.Code);
    }

    [Fact]
    public void Auction_Cancel_ActiveAuction_ShouldRaiseSingleCancelledEvent()
    {
        var auction = CreateActiveAuction();
        auction.ClearDomainEvents();

        var result = auction.Cancel(Now);

        Assert.True(result.IsSuccess);
        var raised = Assert.IsType<AuctionCancelledEvent>(Assert.Single(auction.GetDomainEvents()));
        Assert.Equal(auction.Id, raised.AuctionId);
        Assert.Equal(Now, raised.CancelledAt);
    }

    [Fact]
    public void Auction_Cancel_AlreadyCancelled_ShouldNotRaiseEvent()
    {
        var auction = CreateActiveAuction();
        auction.Cancel(Now);
        auction.ClearDomainEvents();

        auction.Cancel(Now);

        Assert.Empty(auction.GetDomainEvents());
    }

    [Fact]
    public void Auction_Cancel_ClosedAuction_ShouldFailWithoutEvent()
    {
        var auction = CreateActiveAuction();
        auction.Close(Now);
        auction.ClearDomainEvents();

        var result = auction.Cancel(Now);

        Assert.True(result.IsFailure);
        Assert.Empty(auction.GetDomainEvents());
    }
}
