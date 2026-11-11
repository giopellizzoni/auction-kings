using AuctionKings.Application.Abstractions;
using AuctionKings.BackgroundWorkers;
using AuctionKings.Domain.Events;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Shared.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly IOutboxRepository _repository = Substitute.For<IOutboxRepository>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly OutboxOptions _options = new() { BatchSize = 10, MaxRetries = 3 };
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOutboxRepository)).Returns(_repository);
        serviceProvider.GetService(typeof(IEventBus)).Returns(_eventBus);

        scope.ServiceProvider.Returns(serviceProvider);

        // Run the batch delegate inline, as the real repository does inside its transaction.
        _repository
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<CancellationToken, Task>>()(ci.Arg<CancellationToken>()));

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

        await _eventBus.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
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
        _eventBus
            .PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
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
        _eventBus
            .PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
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

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxProcessor_StrategyReplayAfterSaveFailure_ShouldNotRepublish()
    {
        var first = BuildAuctionCreatedMessage();
        var second = BuildAuctionCreatedMessage();
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([first, second]);

        // First save hits a transient DB error; the execution strategy replays the whole unit.
        var saveCalls = 0;
        _repository
            .SaveAsync(Arg.Any<CancellationToken>())
            .Returns(_ => ++saveCalls == 1
                ? Task.FromException(new TimeoutException("transient"))
                : Task.CompletedTask);
        _repository
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var work = ci.Arg<Func<CancellationToken, Task>>();
                try
                {
                    await work(CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    await work(CancellationToken.None); // replay, as EnableRetryOnFailure would
                }
            });

        await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(2, saveCalls);
        await _eventBus.Received(2).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
        Assert.NotNull(first.ProcessedAt);
        Assert.NotNull(second.ProcessedAt);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldRunBatchInsideTransaction()
    {
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([BuildAuctionCreatedMessage()]);

        await _processor.ProcessBatchAsync(CancellationToken.None);

        await _repository.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxProcessor_RelayedEvent_ShouldKeepOriginalEventId()
    {
        var message = BuildAuctionCreatedMessage();
        var originalEventId = System.Text.Json.JsonDocument.Parse(message.Content)
            .RootElement.GetProperty("EventId").GetGuid();
        _repository
            .FetchPendingAsync(_options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([message]);

        await _processor.ProcessBatchAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<IDomainEvent>(e => e.EventId == originalEventId),
            Arg.Any<CancellationToken>());
    }
}
