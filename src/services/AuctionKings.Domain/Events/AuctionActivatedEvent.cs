using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionActivatedEvent(
    Guid AuctionId,
    DateTime ActivatedAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
