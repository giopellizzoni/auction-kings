using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Messaging;
using AuctionKings.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure.Messaging;

public sealed class SqsMessagePublisherTests
{
    private readonly IAmazonSQS _sqsClient = Substitute.For<IAmazonSQS>();
    private readonly TestLogger<SqsMessagePublisher> _logger = new();
    private readonly SqsOptions _sqsOptions = new()
    {
        BidQueueUrl = "https://test-queue-url",
        ServiceURL = "http://localhost:4566"
    };

    private SqsMessagePublisher CreatePublisher() =>
        new(_sqsClient, Options.Create(_sqsOptions), _logger);

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
                r.MessageAttributes["EventType"].StringValue == (typeof(BidPlacedEvent).FullName ?? typeof(BidPlacedEvent).Name) &&
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

        // JsonSerializerDefaults.Web produces camelCase property names
        Assert.True(root.TryGetProperty("auctionId", out var auctionIdElement));
        Assert.Equal(domainEvent.AuctionId.ToString(), auctionIdElement.GetString());

        Assert.True(root.TryGetProperty("bidId", out var bidIdElement));
        Assert.Equal(domainEvent.BidId.ToString(), bidIdElement.GetString());
    }

    [Fact]
    public async Task PublishAsync_WhenSqsThrows_ShouldNotPropagate()
    {
        // Arrange
        _sqsClient
            .SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonSQSException("simulated failure"));

        var publisher = CreatePublisher();
        var domainEvent = CreateBidPlacedEvent();

        // Act
        var exception = await Record.ExceptionAsync(() => publisher.PublishAsync(domainEvent));

        // Assert — exception must be swallowed
        Assert.Null(exception);

        // Assert — warning must be logged
        Assert.True(_logger.HasLog(LogLevel.Warning, "SQS publish failed"));
    }
}
