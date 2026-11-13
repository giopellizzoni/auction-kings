using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionCancelledEvent(
    Guid AuctionId,
    DateTime CancelledAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
