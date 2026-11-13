using AuctionKings.Application.Abstractions;
using AuctionKings.BackgroundWorkers;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly IOutboxRepository _repository = Substitute.For<IOutboxRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly OutboxOptions _options = new() { BatchSize = 10, MaxRetries = 3 };
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly OutboxProcessor _processor;

    private static readonly DateTime FixedNow = new(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    public OutboxProcessorTests()
    {
        _dateTimeProvider.UtcNow.Returns(FixedNow);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOutboxRepository)).Returns(_repository);
        serviceProvider.GetService(typeof(IPublisher)).Returns(_publisher);
        serviceProvider.GetService(typeof(IEventBus)).Returns(_eventBus);

        scope.ServiceProvider.Returns(serviceProvider);

        var asyncServiceScope = new AsyncServiceScope(scope);
        scopeFactory.CreateAsyncScope().Returns(asyncServiceScope);

        _processor = new OutboxProcessor(
            scopeFactory,
            Options.Create(_options),
            _dateTimeProvider,
            NullLogger<OutboxProcessor>.Instance
        );
    }

    public void Dispose() => _processor.Dispose();

    private static OutboxMessage BuildAuctionCreatedMessage()
    {
        var domainEvent = new AuctionCreatedEvent(
            Guid.NewGuid(),
            "Test Auction",
            Guid.NewGuid(),
            new Money(100m, "USD"),
            FixedNow,
            FixedNow.AddDays(1)
        );

        return OutboxMessage.Create(domainEvent);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldProcessPendingMessages()
    {
        var message = BuildAuctionCreatedMessage();
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([message]);

        await _processor.ProcessBatchAsync(CancellationToken.None);

        await _publisher.Received(1).Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
        Assert.Equal(FixedNow, message.ProcessedAt);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxProcessor_ShouldStampProcessedAtFromDateTimeProvider()
    {
        var message = BuildAuctionCreatedMessage();
        var processedAt = new DateTime(2041, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow.Returns(processedAt);
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([message]);

        await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(processedAt, message.ProcessedAt);
    }

    [Fact]
    public async Task OutboxProcessor_FailedMessage_ShouldRetry()
    {
        var message = BuildAuctionCreatedMessage();
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([message]);
        _publisher
            .Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Transient failure"));

        await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, message.RetryCount);
        Assert.False(message.Failed);
        Assert.Null(message.ProcessedAt);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxProcessor_MaxRetriesExceeded_ShouldMarkAsFailed()
    {
        var message = BuildAuctionCreatedMessage();
        _publisher
            .Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Persistent failure"));

        // Process batch three times, simulating three failure attempts
        for (var i = 0; i < _options.MaxRetries; i++)
        {
            _repository
                .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
                .Returns([message]);

            await _processor.ProcessBatchAsync(CancellationToken.None);
        }

        Assert.True(message.Failed);
        Assert.Equal(_options.MaxRetries, message.RetryCount);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task OutboxProcessor_EmptyBatch_ShouldNotPublish()
    {
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([]);

        await _processor.ProcessBatchAsync(CancellationToken.None);

        await _publisher.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
