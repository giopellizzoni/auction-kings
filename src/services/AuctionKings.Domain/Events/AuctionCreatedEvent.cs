using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionCreatedEvent(
    Guid AuctionId,
    string Title,
    Guid SellerId,
    Money StartingPrice,
    DateTime StartTime,
    DateTime EndTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
