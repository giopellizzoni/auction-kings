using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AuctionKings.Application.Abstractions;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuctionKings.Infrastructure.Messaging;

public sealed partial class SqsMessagePublisher(
    IAmazonSQS sqsClient,
    IOptions<SqsOptions> options,
    ILogger<SqsMessagePublisher> logger) : IEventBus
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SqsOptions _options = options.Value;

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);

        var request = new SendMessageRequest
        {
            QueueUrl = _options.BidQueueUrl,
            MessageBody = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = domainEvent.GetType().FullName ?? domainEvent.GetType().Name
                },
                ["EventId"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = domainEvent.EventId.ToString()
                }
            }
        };

        try
        {
            await sqsClient.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var eventType = domainEvent.GetType().Name;
            LogPublishFailed(logger, ex, eventType, domainEvent.EventId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SQS publish failed for event {EventType} {EventId}.")]
    private static partial void LogPublishFailed(ILogger logger, Exception ex, string eventType, Guid eventId);
}
