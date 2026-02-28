using AuctionKings.Domain.Enums;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Aggregates;

public sealed class Bid : Entity
{
    public Guid AuctionId { get; private set; }
    public Guid BidderId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime PlacedAt { get; private set; }
    public BidStatus Status { get; private set; }

    private Bid() : base(Guid.NewGuid()) { }

    internal static Bid Create(Guid auctionId, Guid bidderId, Money amount, DateTime placedAt)
    {
        return new Bid
        {
            AuctionId = auctionId,
            BidderId = bidderId,
            Amount = amount,
            PlacedAt = placedAt,
            Status = BidStatus.Active,
        };
    }

    internal void MarkAsOutbid() => Status = BidStatus.Outbid;
    internal void MarkAsWinning() => Status = BidStatus.Winning;
    internal void MarkAsWon() => Status = BidStatus.Won;
    internal void Cancel() => Status = BidStatus.Cancelled;
}
