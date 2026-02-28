using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionClosedEvent(
    Guid AuctionId,
    DateTime ClosedAt,
    Guid? WinnerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
