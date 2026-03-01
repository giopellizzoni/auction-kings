using System.Text.Json;
using AuctionKings.Shared.Common;

namespace AuctionKings.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(IDomainEvent domainEvent)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = domainEvent.GetType().FullName!,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOn = domainEvent.OccurredOn,
        };
    }

    public void MarkAsProcessed(DateTime processedAt) => ProcessedAt = processedAt;
}
