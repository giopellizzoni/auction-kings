namespace AuctionKings.Infrastructure.Messaging;

public sealed class SqsOptions
{
    /// <summary>
    /// Configuration section. As environment variables: <c>AWS__SQS__BidQueueUrl</c>, <c>AWS__SQS__ServiceURL</c>.
    /// </summary>
    public const string SectionName = "AWS:SQS";

    public string BidQueueUrl { get; init; } = string.Empty;

    /// <summary>Endpoint override for LocalStack. Leave unset on AWS so the SDK uses the regional endpoint.</summary>
    public string? ServiceURL { get; init; }
}
