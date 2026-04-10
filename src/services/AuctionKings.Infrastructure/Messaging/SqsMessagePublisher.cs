using Amazon.SQS;
using Amazon.SQS.Model;
using AuctionKings.Application.Abstractions;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AuctionKings.Infrastructure.Messaging;

public sealed class SqsMessagePublisher(IAmazonSQS sqsClient, IOptions<SqsOptions> options) : IEventBus
{
    private readonly SqsOptions _options = options.Value;

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var messageBody = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

        var request = new SendMessageRequest
        {
            QueueUrl = _options.BidQueueUrl,
            MessageBody = messageBody,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = domainEvent.GetType().FullName
                },
                ["EventId"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = domainEvent.EventId.ToString()
                }
            }
        };

        await sqsClient.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
