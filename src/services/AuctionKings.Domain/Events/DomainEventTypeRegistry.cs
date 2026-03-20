using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Events;

public static class DomainEventTypeRegistry
{
    private static readonly Dictionary<string, Type> Registry = new()
    {
        [typeof(AuctionCreatedEvent).FullName!] = typeof(AuctionCreatedEvent),
        [typeof(AuctionClosedEvent).FullName!] = typeof(AuctionClosedEvent),
        [typeof(AuctionExtendedEvent).FullName!] = typeof(AuctionExtendedEvent),
        [typeof(BidPlacedEvent).FullName!] = typeof(BidPlacedEvent),
        [typeof(WinnerDeterminedEvent).FullName!] = typeof(WinnerDeterminedEvent),
    };

    public static Type? Resolve(string typeName)
    {
        Registry.TryGetValue(typeName, out var type);
        return type;
    }

    public static IReadOnlyDictionary<string, Type> All => Registry;
}
