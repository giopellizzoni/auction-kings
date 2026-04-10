using Amazon.SQS;
using Amazon.SQS.Model;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;

namespace AuctionKings.UnitTests.Infrastructure.Messaging;

public sealed class SqsMessagePublisherTests
{
    private readonly IAmazonSQS _sqsClient = Substitute.For<IAmazonSQS>();
    private readonly SqsOptions _sqsOptions = new()
    {
        BidQueueUrl = "https://test-queue-url",
        ServiceURL = "http://localhost:4566"
    };

    private SqsMessagePublisher CreatePublisher() =>
        new(_sqsClient, Options.Create(_sqsOptions));

    private static BidPlacedEvent CreateBidPlacedEvent() =>
        new(
            AuctionId: Guid.NewGuid(),
            BidId: Guid.NewGuid(),
            BidderId: Guid.NewGuid(),
            Amount: new Money(150m, "USD"),
            PlacedAt: DateTime.UtcNow
        );

    [Fact]
    public async Task PublishAsync_ShouldSendMessageToCorrectQueueUrl()
    {
        var publisher = CreatePublisher();
        var domainEvent = CreateBidPlacedEvent();

        await publisher.PublishAsync(domainEvent);

        await _sqsClient.Received(1).SendMessageAsync(
            Arg.Is<SendMessageRequest>(r => r.QueueUrl == "https://test-queue-url"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_ShouldIncludeEventTypeAndEventIdInMessageAttributes()
    {
        var publisher = CreatePublisher();
        var domainEvent = CreateBidPlacedEvent();

        await publisher.PublishAsync(domainEvent);

        await _sqsClient.Received(1).SendMessageAsync(
            Arg.Is<SendMessageRequest>(r =>
                r.MessageAttributes.ContainsKey("EventType") &&
                r.MessageAttributes["EventType"].DataType == "String" &&
                r.MessageAttributes["EventType"].StringValue == typeof(BidPlacedEvent).FullName &&
                r.MessageAttributes.ContainsKey("EventId") &&
                r.MessageAttributes["EventId"].DataType == "String" &&
                r.MessageAttributes["EventId"].StringValue == domainEvent.EventId.ToString()
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeEventAsJsonBody()
    {
        var publisher = CreatePublisher();
        var domainEvent = CreateBidPlacedEvent();

        SendMessageRequest? capturedRequest = null;
        await _sqsClient.SendMessageAsync(
            Arg.Do<SendMessageRequest>(r => capturedRequest = r),
            Arg.Any<CancellationToken>()
        );

        await publisher.PublishAsync(domainEvent);

        Assert.NotNull(capturedRequest);
        var body = capturedRequest.MessageBody;

        // Must be valid JSON
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Must contain auction-specific fields
        Assert.True(root.TryGetProperty("AuctionId", out var auctionIdElement));
        Assert.Equal(domainEvent.AuctionId.ToString(), auctionIdElement.GetString());

        Assert.True(root.TryGetProperty("BidId", out var bidIdElement));
        Assert.Equal(domainEvent.BidId.ToString(), bidIdElement.GetString());
    }
}
