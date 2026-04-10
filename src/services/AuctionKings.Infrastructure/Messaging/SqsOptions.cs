namespace AuctionKings.Infrastructure.Messaging;

public sealed class SqsOptions
{
    public string BidQueueUrl { get; init; } = string.Empty;
    public string? ServiceURL { get; init; }
}
