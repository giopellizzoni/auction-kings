using AuctionKings.Domain.Events;

namespace AuctionKings.UnitTests.Domain;

public sealed class DomainEventTypeRegistryTests
{
    [Theory]
    [InlineData(typeof(AuctionCreatedEvent))]
    [InlineData(typeof(AuctionClosedEvent))]
    [InlineData(typeof(AuctionCancelledEvent))]
    [InlineData(typeof(AuctionExtendedEvent))]
    [InlineData(typeof(AuctionActivatedEvent))]
    [InlineData(typeof(BidPlacedEvent))]
    [InlineData(typeof(WinnerDeterminedEvent))]
    public void DomainEventTypeRegistry_ShouldResolveKnownTypes(Type expectedType)
    {
        var typeName = expectedType.FullName!;

        var resolved = DomainEventTypeRegistry.Resolve(typeName);

        Assert.NotNull(resolved);
        Assert.Equal(expectedType, resolved);
    }

    [Fact]
    public void DomainEventTypeRegistry_UnknownType_ShouldReturnNull()
    {
        var resolved = DomainEventTypeRegistry.Resolve("AuctionKings.Domain.Events.NonExistentEvent");

        Assert.Null(resolved);
    }

    [Fact]
    public void DomainEventTypeRegistry_All_ShouldContainAllExpectedTypes()
    {
        var all = DomainEventTypeRegistry.All;

        Assert.Contains(typeof(AuctionCreatedEvent).FullName!, all.Keys);
        Assert.Contains(typeof(AuctionClosedEvent).FullName!, all.Keys);
        Assert.Contains(typeof(AuctionCancelledEvent).FullName!, all.Keys);
        Assert.Contains(typeof(AuctionExtendedEvent).FullName!, all.Keys);
        Assert.Contains(typeof(AuctionActivatedEvent).FullName!, all.Keys);
        Assert.Contains(typeof(BidPlacedEvent).FullName!, all.Keys);
        Assert.Contains(typeof(WinnerDeterminedEvent).FullName!, all.Keys);
        Assert.Equal(7, all.Count);
    }
}
