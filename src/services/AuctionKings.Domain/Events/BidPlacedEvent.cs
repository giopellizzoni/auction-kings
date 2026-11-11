using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record BidPlacedEvent(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    Money Amount,
    DateTime PlacedAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
