namespace AuctionKings.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalSeconds { get; init; } = 1;
    public int BatchSize { get; init; } = 20;
    public int MaxRetries { get; init; } = 3;
}
