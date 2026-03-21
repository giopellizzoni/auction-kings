using AuctionKings.BackgroundWorkers;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
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
    private readonly OutboxOptions _options = new() { BatchSize = 10, MaxRetries = 3 };
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOutboxRepository)).Returns(_repository);
        serviceProvider.GetService(typeof(IPublisher)).Returns(_publisher);

        scope.ServiceProvider.Returns(serviceProvider);

        var asyncServiceScope = new AsyncServiceScope(scope);
        scopeFactory.CreateAsyncScope().Returns(asyncServiceScope);

        _processor = new OutboxProcessor(
            scopeFactory,
            Options.Create(_options),
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
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1)
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
        Assert.NotNull(message.ProcessedAt);
        await _repository.Received(1).SaveAsync(Arg.Any<CancellationToken>());
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
