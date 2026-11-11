using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record AuctionExtendedEvent(
    Guid AuctionId,
    DateTime NewEndTime,
    TimeSpan ExtendedBy) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
