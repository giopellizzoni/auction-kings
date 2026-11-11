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
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }

        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("SellerId cannot be empty.", nameof(sellerId));
        }

        var endTime = duration.GetEndTime(startTime);

        // Auctions whose StartTime is in the past (or now) become Active immediately;
        // future-dated auctions start as Pending and are activated by AuctionActivationWorker.
        var status = startTime <= DateTime.UtcNow ? AuctionStatus.Active : AuctionStatus.Pending;

        var auction = new Auction
        {
            Title = title,
            Description = description ?? string.Empty,
            StartingPrice = startingPrice,
            CurrentPrice = startingPrice,
            StartTime = startTime,
            EndTime = endTime,
            Status = status,
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

    public Result Activate(DateTime now)
    {
        if (Status == AuctionStatus.Active)
        {
            return Result.Success();
        }

        if (Status != AuctionStatus.Pending)
        {
            return Result.Failure(DomainErrors.Auction.NotActive);
        }

        if (now < StartTime)
        {
            return Result.Success();
        }

        Status = AuctionStatus.Active;
        Version++;

        AddDomainEvent(new AuctionActivatedEvent(Id, now));

        return Result.Success();
    }

    public Result<Bid> PlaceBid(Guid bidderId, Money amount, DateTime now)
    {
        if (Status != AuctionStatus.Active)
        {
            return Result.Failure<Bid>(DomainErrors.Auction.NotActive);
        }

        if (now >= EndTime)
        {
            return Result.Failure<Bid>(DomainErrors.Auction.Ended);
        }

        if (bidderId == SellerId)
        {
            return Result.Failure<Bid>(DomainErrors.Auction.CannotBidOnOwnAuction);
        }

        if (!string.Equals(amount.Currency, CurrentPrice.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Bid>(DomainErrors.Bid.CurrencyMismatch);
        }

        var currentWinningBid = _bids.FirstOrDefault(b => b.Status == BidStatus.Winning);

        if (currentWinningBid?.BidderId == bidderId)
        {
            return Result.Failure<Bid>(DomainErrors.Auction.CannotOutbidSelf);
        }

        var minimumBid = BidIncrement.GetMinimumBid(CurrentPrice);
        if (amount < minimumBid)
        {
            return Result.Failure<Bid>(DomainErrors.Auction.BidTooLow);
        }

        currentWinningBid?.MarkAsOutbid();

        var bid = Bid.Create(Id, bidderId, amount, now);
        bid.MarkAsWinning();
        _bids.Add(bid);

        CurrentPrice = amount;
        Version++;

        AddDomainEvent(new BidPlacedEvent(Id, bid.Id, bidderId, amount, now));

        var timeRemaining = EndTime - now;
        if (timeRemaining < AntiSnipingThreshold)
        {
            EndTime = EndTime.Add(AntiSnipingExtension);
            AddDomainEvent(new AuctionExtendedEvent(Id, EndTime, AntiSnipingExtension));
        }

        return Result.Success(bid);
    }

    /// <summary>
    /// Closes the auction and determines the winner.
    /// </summary>
    /// <param name="now">Current server time.</param>
    /// <param name="force">
    /// When <c>false</c> (the scheduled-close path) the auction must have reached its
    /// <see cref="EndTime"/>. This re-check matters because a last-second bid can extend
    /// <see cref="EndTime"/> (anti-sniping) between the moment a worker selects the auction as
    /// expired and the moment it reloads and closes it. Pass <c>true</c> only for an explicit
    /// administrative early close.
    /// </param>
    public Result Close(DateTime now, bool force = false)
    {
        if (Status == AuctionStatus.Closed)
        {
            return Result.Failure(DomainErrors.Auction.AlreadyClosed);
        }

        if (Status == AuctionStatus.Cancelled)
        {
            return Result.Failure(DomainErrors.Auction.AlreadyCancelled);
        }

        if (!force && now < EndTime)
        {
            return Result.Failure(DomainErrors.Auction.NotYetEnded);
        }

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
        {
            return Result.Failure(DomainErrors.Auction.AlreadyClosed);
        }

        if (Status == AuctionStatus.Cancelled)
        {
            return Result.Failure(DomainErrors.Auction.AlreadyCancelled);
        }

        Status = AuctionStatus.Cancelled;
        Version++;

        foreach (var bid in _bids.Where(b => b.Status is BidStatus.Active or BidStatus.Winning))
        {
            bid.Cancel();
        }

        return Result.Success();
    }
}
