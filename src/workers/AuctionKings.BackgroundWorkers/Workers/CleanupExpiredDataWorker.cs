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

    /// <summary>
    /// Idempotency reservations still "Reserved" after this long are considered abandoned (the owning
    /// request crashed) and are deleted. This sweep is only housekeeping: a client retrying with the same
    /// key does not wait for it — IdempotencyService.TryReserveAsync reclaims a stale reservation on the
    /// spot. Keep this aligned with IdempotencyService.StaleReservationAge.
    /// </summary>
    public int StaleReservationMinutes { get; init; } = 5;
}

public sealed partial class CleanupExpiredDataWorker : BackgroundService
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
            // An exception escaping ExecuteAsync stops the whole host (BackgroundServiceExceptionBehavior.StopHost),
            // taking the outbox and auction workers down with it — so every iteration is isolated.
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                LogUnexpectedLoopError(_logger, ex);
            }

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
            LogIdempotencyKeysDeleted(_logger, keysDeleted);
        }

        var staleReservations = await cleanup.DeleteStaleIdempotencyReservationsAsync(
            TimeSpan.FromMinutes(_options.StaleReservationMinutes),
            cancellationToken);
        if (staleReservations > 0)
        {
            LogStaleReservationsDeleted(_logger, staleReservations);
        }

        var retention = TimeSpan.FromDays(_options.OutboxRetentionDays);
        var messagesDeleted = await cleanup.DeleteOldOutboxMessagesAsync(retention, cancellationToken);
        if (messagesDeleted > 0)
        {
            LogOutboxMessagesDeleted(_logger, messagesDeleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired idempotency key(s).")]
    private static partial void LogIdempotencyKeysDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleted {Count} abandoned idempotency reservation(s).")]
    private static partial void LogStaleReservationsDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} old outbox message(s).")]
    private static partial void LogOutboxMessagesDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error in cleanup worker loop. Continuing after delay.")]
    private static partial void LogUnexpectedLoopError(ILogger logger, Exception ex);
}
