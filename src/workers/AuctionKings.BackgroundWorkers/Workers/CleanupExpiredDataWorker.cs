using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers.Workers;

public sealed class CleanupWorkerOptions
{
    public const string SectionName = "CleanupWorker";
    public int IntervalHours { get; init; } = 1;
    public int OutboxRetentionDays { get; init; } = 7;
}

public sealed class CleanupExpiredDataWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CleanupWorkerOptions _options;
    private readonly ILogger<CleanupExpiredDataWorker> _logger;

    public CleanupExpiredDataWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<CleanupWorkerOptions> options,
        ILogger<CleanupExpiredDataWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }
    }

    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var keysDeleted = await cleanup.DeleteExpiredIdempotencyKeysAsync(cancellationToken);
        if (keysDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} expired idempotency key(s).", keysDeleted);
        }

        var retention = TimeSpan.FromDays(_options.OutboxRetentionDays);
        var messagesDeleted = await cleanup.DeleteOldOutboxMessagesAsync(retention, cancellationToken);
        if (messagesDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old outbox message(s).", messagesDeleted);
        }
    }
}
