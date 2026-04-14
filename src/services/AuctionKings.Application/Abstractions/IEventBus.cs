using AuctionKings.Shared.Common;

namespace AuctionKings.Application.Abstractions;

public interface IEventBus
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
