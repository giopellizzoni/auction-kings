using System.Text.Json;
using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Events;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers;

public sealed partial class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await repository.FetchPendingAsync(_options.BatchSize, cancellationToken);
        OutboxMetrics.SetPending(messages.Count);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, publisher, eventBus, cancellationToken);
        }

        await repository.SaveAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IPublisher publisher,
        IEventBus eventBus,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var eventType = DomainEventTypeRegistry.Resolve(message.Type);

            if (eventType is null)
            {
                LogUnknownType(_logger, message.Type, message.Id);
                message.MarkAsFailed();
                return;
            }

            var domainEvent = JsonSerializer.Deserialize(message.Content, eventType);

            if (domainEvent is null)
            {
                LogDeserializeFailed(_logger, message.Id, message.Type);
                message.MarkAsFailed();
                return;
            }

            await publisher.Publish(domainEvent, cancellationToken);

            await eventBus.PublishAsync((IDomainEvent)domainEvent, cancellationToken).ConfigureAwait(false);

            message.MarkAsProcessed(DateTime.UtcNow);

            LogProcessed(_logger, message.Id, message.Type);
        }
        catch (Exception ex)
        {
            message.IncrementRetry();

            LogProcessFailed(_logger, ex, message.Id, message.RetryCount);

            if (message.RetryCount >= _options.MaxRetries)
            {
                message.MarkAsFailed();

                LogMaxRetriesExceeded(_logger, message.Id, _options.MaxRetries);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown outbox message type '{Type}' for message {Id}. Marking as failed.")]
    private static partial void LogUnknownType(ILogger logger, string type, Guid id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize outbox message {Id} of type '{Type}'. Marking as failed.")]
    private static partial void LogDeserializeFailed(ILogger logger, Guid id, string type);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox message {Id} of type '{Type}' processed successfully.")]
    private static partial void LogProcessed(ILogger logger, Guid id, string type);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process outbox message {Id} (attempt {RetryCount}).")]
    private static partial void LogProcessFailed(ILogger logger, Exception ex, Guid id, int retryCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {Id} exceeded max retries ({MaxRetries}). Marked as failed.")]
    private static partial void LogMaxRetriesExceeded(ILogger logger, Guid id, int maxRetries);
}
