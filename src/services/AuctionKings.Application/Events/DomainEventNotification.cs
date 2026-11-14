using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Events;

/// <summary>
/// MediatR adapter for domain events. The Domain layer must not reference MediatR, so domain
/// events are not <see cref="INotification"/>; <c>IPublisher.Publish(object)</c> rejects them.
/// Handlers implement <c>INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;</c>.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;

public static class DomainEventNotification
{
    /// <summary>Wraps a runtime-typed domain event in its closed <see cref="DomainEventNotification{TEvent}"/>.</summary>
    public static INotification Wrap(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var closed = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(closed, domainEvent)!;
    }
}
