using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionClosedEvent(
    Guid AuctionId,
    DateTime ClosedAt,
    Guid? WinnerId) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
