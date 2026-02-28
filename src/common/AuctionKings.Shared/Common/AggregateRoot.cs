namespace AuctionKings.Shared.Common;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id)
        : base(id) { }

    // Required for EF Core
    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Returns all accumulated domain events and clears the internal list.
    /// Call this in the UnitOfWork to dispatch events before clearing.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> ClearDomainEvents()
    {
        var events = _domainEvents.AsReadOnly().ToList();
        _domainEvents.Clear();
        return events;
    }
}
