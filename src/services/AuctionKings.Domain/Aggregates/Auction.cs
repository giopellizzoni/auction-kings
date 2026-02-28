using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Aggregates;

public sealed class Auction : AggregateRoot
{
    private static readonly TimeSpan AntiSnipingThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AntiSnipingExtension = TimeSpan.FromMinutes(5);

    private readonly List<Bid> _bids = [];

    public string Title { get; private set; }
    public string Description { get; private set; }
    public Money StartingPrice { get; private set; }
    public Money CurrentPrice { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public AuctionStatus Status { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid? WinnerId { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyList<Bid> Bids => _bids.AsReadOnly();

    private Auction() : base(Guid.NewGuid()) { }

    public static Auction Create(
        string title,
        string description,
        Money startingPrice,
        DateTime startTime,
        AuctionDuration duration,
        Guid sellerId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (sellerId == Guid.Empty)
            throw new ArgumentException("SellerId cannot be empty.", nameof(sellerId));

        var endTime = duration.GetEndTime(startTime);

        var auction = new Auction
        {
            Title = title,
            Description = description ?? string.Empty,
            StartingPrice = startingPrice,
            CurrentPrice = startingPrice,
            StartTime = startTime,
            EndTime = endTime,
            Status = AuctionStatus.Pending,
            SellerId = sellerId,
            Version = 0,
        };

        auction.AddDomainEvent(new AuctionCreatedEvent(
            auction.Id,
            title,
            sellerId,
            startingPrice,
            startTime,
            endTime));

        return auction;
    }

    public void Activate(DateTime now)
    {
        if (Status != AuctionStatus.Pending)
            return;

        Status = AuctionStatus.Active;
    }

    public Result<Bid> PlaceBid(Guid bidderId, Money amount, DateTime now)
    {
        if (Status != AuctionStatus.Active)
            return Result.Failure<Bid>(DomainErrors.Auction.NotActive);

        if (bidderId == SellerId)
            return Result.Failure<Bid>(DomainErrors.Auction.CannotBidOnOwnAuction);

        var minimumBid = BidIncrement.GetMinimumBid(CurrentPrice);
        if (amount < minimumBid)
            return Result.Failure<Bid>(DomainErrors.Auction.BidTooLow);

        // Mark current winning bid as outbid
        var currentWinningBid = _bids.FirstOrDefault(b => b.Status == BidStatus.Winning);
        currentWinningBid?.MarkAsOutbid();

        var bid = Bid.Create(Id, bidderId, amount, now);
        bid.MarkAsWinning();
        _bids.Add(bid);

        CurrentPrice = amount;
        Version++;

        AddDomainEvent(new BidPlacedEvent(Id, bid.Id, bidderId, amount, now));

        // Anti-sniping: extend if bid placed within threshold of end time
        var timeRemaining = EndTime - now;
        if (timeRemaining < AntiSnipingThreshold)
        {
            EndTime = EndTime.Add(AntiSnipingExtension);
            AddDomainEvent(new AuctionExtendedEvent(Id, EndTime, AntiSnipingExtension));
        }

        return Result.Success(bid);
    }

    public Result Close(DateTime now)
    {
        if (Status == AuctionStatus.Closed)
            return Result.Failure(DomainErrors.Auction.AlreadyClosed);

        if (Status == AuctionStatus.Cancelled)
            return Result.Failure(DomainErrors.Auction.AlreadyCancelled);

        Status = AuctionStatus.Closed;
        Version++;

        var winningBid = _bids.FirstOrDefault(b => b.Status == BidStatus.Winning);

        if (winningBid is not null)
        {
            winningBid.MarkAsWon();
            WinnerId = winningBid.BidderId;

            AddDomainEvent(new AuctionClosedEvent(Id, now, WinnerId));
            AddDomainEvent(new WinnerDeterminedEvent(Id, winningBid.BidderId, winningBid.Amount));
        }
        else
        {
            AddDomainEvent(new AuctionClosedEvent(Id, now, null));
        }

        return Result.Success();
    }

    public Result Cancel(DateTime now)
    {
        if (Status == AuctionStatus.Closed)
            return Result.Failure(DomainErrors.Auction.AlreadyClosed);

        Status = AuctionStatus.Cancelled;
        Version++;

        foreach (var bid in _bids.Where(b => b.Status is BidStatus.Active or BidStatus.Winning))
            bid.Cancel();

        return Result.Success();
    }
}
