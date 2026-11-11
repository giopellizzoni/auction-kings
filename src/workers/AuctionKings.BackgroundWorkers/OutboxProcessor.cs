using System.Text.Json;
using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Events;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Shared.Common;
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
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogUnexpectedLoopError(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // The execution strategy may replay the whole unit after a transient DB failure (e.g. on
        // SaveAsync, after events were already sent). Remember what this batch already published so a
        // replay only re-marks those rows instead of re-sending them — otherwise one transient error
        // would duplicate the entire batch on SQS up to the strategy's retry count.
        var published = new HashSet<Guid>();

        // The transaction keeps the FOR UPDATE SKIP LOCKED row locks for the full batch, so a
        // second worker cannot pick up the same rows. It must be opened through the execution
        // strategy because the DbContext is configured with EnableRetryOnFailure.
        await repository.ExecuteInTransactionAsync(
            async ct =>
            {
                var messages = await repository.FetchPendingAsync(_options.BatchSize, ct);
                OutboxMetrics.SetPending(messages.Count);

                if (messages.Count == 0)
                {
                    return;
                }

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(message, eventBus, published, ct);
                }

                await repository.SaveAsync(ct);
            },
            cancellationToken);
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IEventBus eventBus,
        HashSet<Guid> published,
        CancellationToken cancellationToken
    )
    {
        if (published.Contains(message.Id))
        {
            // Sent during an earlier attempt of this batch that was rolled back; only re-mark it.
            message.MarkAsProcessed(DateTime.UtcNow);
            return;
        }

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

            // Domain events are not MediatR notifications (IPublisher.Publish(object) would throw)
            // and there are no in-process handlers, so the outbox only relays to the event bus.
            await eventBus.PublishAsync((IDomainEvent)domainEvent, cancellationToken).ConfigureAwait(false);
            published.Add(message.Id);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error in outbox processor loop. Continuing after delay.")]
    private static partial void LogUnexpectedLoopError(ILogger logger, Exception ex);
}
