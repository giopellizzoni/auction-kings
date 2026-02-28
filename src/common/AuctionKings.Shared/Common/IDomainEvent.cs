namespace AuctionKings.Shared.Common;

/// <summary>
/// Marker interface for domain events.
/// All properties are serialization-friendly for Outbox Pattern compatibility.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique identifier for the event instance. Used for deduplication in consumers.</summary>
    Guid EventId { get; }

    /// <summary>Server-authoritative timestamp — never trust client clocks.</summary>
    DateTime OccurredOn { get; }
}
