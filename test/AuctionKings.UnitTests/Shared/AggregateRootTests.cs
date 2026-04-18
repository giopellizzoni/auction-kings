using AuctionKings.Shared.Common;

namespace AuctionKings.UnitTests.Common;

public class AggregateRootTests
{
    private sealed class TestEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
        public string Payload { get; }

        public TestEvent(string payload) => Payload = payload;
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public TestAggregate(Guid id)
            : base(id) { }

        public void RaiseSomething(string payload) => AddDomainEvent(new TestEvent(payload));
    }

    [Fact]
    public void AggregateRoot_AddDomainEvent_ShouldStoreEvent()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.RaiseSomething("test-payload");

        var events = aggregate.GetDomainEvents();
        Assert.Single(events);
        var evt = Assert.IsType<TestEvent>(events.First());
        Assert.Equal("test-payload", evt.Payload);
    }

    [Fact]
    public void AggregateRoot_ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething("a");
        aggregate.RaiseSomething("b");

        var cleared = aggregate.ClearDomainEvents();

        Assert.Equal(2, cleared.Count);
        Assert.Empty(aggregate.GetDomainEvents());
    }

    [Fact]
    public void AggregateRoot_ClearDomainEvents_ShouldReturnSnapshotBeforeClear()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething("event-1");

        var returned = aggregate.ClearDomainEvents();
        var afterClear = aggregate.GetDomainEvents();

        Assert.Single(returned);
        Assert.Empty(afterClear);
    }

    [Fact]
    public void AggregateRoot_GetDomainEvents_ShouldBeReadOnly()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        var events = aggregate.GetDomainEvents();

        Assert.IsAssignableFrom<IReadOnlyCollection<IDomainEvent>>(events);
    }
}
