using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public sealed record WinnerDeterminedEvent(
    Guid AuctionId,
    Guid WinnerId,
    Money WinningAmount) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
