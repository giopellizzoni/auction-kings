using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Realtime;
using StackExchange.Redis;

namespace AuctionKings.API.Realtime;

/// <summary>
/// Consumes real-time notifications raised by other processes (the background worker — e.g. an
/// auction closed by AuctionCloseWorker) from <see cref="RealtimeNotificationChannel"/> and pushes
/// them to SignalR clients.
/// <para>
/// Every API replica joins the same consumer group, so each entry is handled by exactly one
/// replica; the SignalR Redis backplane then fans it out to clients connected to any replica.
/// Entries are acknowledged immediately after dispatch — these are ephemeral UI pushes, so an
/// entry lost to a crash mid-dispatch is acceptable (clients resync via RequestStateSync).
/// </para>
/// </summary>
internal sealed partial class RealtimeNotificationRelay(
    IConnectionMultiplexer redis,
    IRealtimeNotificationService notifications,
    ILogger<RealtimeNotificationRelay> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan MinIdleDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxIdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(2);

    // Maintenance: reclaim entries stranded with a dead consumer, and drop dead consumers.
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StrandedEntryAge = TimeSpan.FromMinutes(1);

    // A live relay polls (XREADGROUP) at least every MaxIdleDelay, which resets its consumer idle
    // time, so a consumer idle this long belongs to a task that crashed or was replaced.
    internal static readonly TimeSpan DeadConsumerIdle = TimeSpan.FromMinutes(10);

    private readonly string _consumerName = $"{Environment.MachineName}-{Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var groupReady = false;
        var idleDelay = MinIdleDelay;
        var nextMaintenance = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();

                if (!groupReady)
                {
                    await EnsureConsumerGroupAsync(db).ConfigureAwait(false);
                    groupReady = true;
                }

                if (DateTime.UtcNow >= nextMaintenance)
                {
                    await RunMaintenanceAsync(db, stoppingToken).ConfigureAwait(false);
                    nextMaintenance = DateTime.UtcNow + MaintenanceInterval;
                }

                var handled = await ProcessBatchAsync(db, stoppingToken).ConfigureAwait(false);
                if (handled == 0)
                {
                    // No XREADGROUP BLOCK in StackExchange.Redis (multiplexed connection), so poll,
                    // backing off while the stream is quiet to keep idle Redis traffic low.
                    await Task.Delay(idleDelay, stoppingToken).ConfigureAwait(false);
                    idleDelay = NextIdleDelay(idleDelay);
                }
                else
                {
                    idleDelay = MinIdleDelay;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // e.g. Redis unavailable, or the stream/group was deleted (NOGROUP) — recreate on next pass.
                groupReady = false;
                LogRelayError(logger, ex);
                await Task.Delay(ErrorDelay, stoppingToken).ConfigureAwait(false);
            }
        }

        await RemoveOwnConsumerAsync().ConfigureAwait(false);
    }

    internal static TimeSpan NextIdleDelay(TimeSpan current) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, MaxIdleDelay.Ticks));

    internal async Task<int> ProcessBatchAsync(IDatabase db, CancellationToken cancellationToken)
    {
        var entries = await db.StreamReadGroupAsync(
            RealtimeNotificationChannel.Key,
            RealtimeNotificationChannel.ConsumerGroup,
            _consumerName,
            StreamPosition.NewMessages,
            BatchSize).ConfigureAwait(false);

        await DispatchEntriesAsync(db, entries, cancellationToken).ConfigureAwait(false);
        return entries.Length;
    }

    private async Task DispatchEntriesAsync(IDatabase db, StreamEntry[] entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var notification = RealtimeNotificationChannel.Deserialize(entry[RealtimeNotificationChannel.PayloadField]);
                if (notification is not null)
                {
                    await DispatchAsync(notification, notifications, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDispatchFailed(logger, entry.Id.ToString(), ex);
            }

            await db.StreamAcknowledgeAsync(
                RealtimeNotificationChannel.Key,
                RealtimeNotificationChannel.ConsumerGroup,
                entry.Id).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A replica that crashes between XREADGROUP and XACK leaves entries in its pending list, and every
    /// task restart leaves a consumer behind (names are per process). Claim stranded entries for this
    /// consumer (XAUTOCLAIM) and delete consumers that are long idle with nothing pending.
    /// </summary>
    private async Task RunMaintenanceAsync(IDatabase db, CancellationToken cancellationToken)
    {
        var claimed = await db.StreamAutoClaimAsync(
            RealtimeNotificationChannel.Key,
            RealtimeNotificationChannel.ConsumerGroup,
            _consumerName,
            (long)StrandedEntryAge.TotalMilliseconds,
            "0-0",
            BatchSize).ConfigureAwait(false);

        if (claimed.ClaimedEntries.Length > 0)
        {
            LogReclaimedEntries(logger, claimed.ClaimedEntries.Length);
            await DispatchEntriesAsync(db, claimed.ClaimedEntries, cancellationToken).ConfigureAwait(false);
        }

        var consumers = await db.StreamConsumerInfoAsync(
            RealtimeNotificationChannel.Key,
            RealtimeNotificationChannel.ConsumerGroup).ConfigureAwait(false);

        foreach (var consumer in consumers)
        {
            if (IsDeadConsumer(consumer.Name, consumer.PendingMessageCount, consumer.IdleTimeInMilliseconds, _consumerName))
            {
                await db.StreamDeleteConsumerAsync(
                    RealtimeNotificationChannel.Key,
                    RealtimeNotificationChannel.ConsumerGroup,
                    consumer.Name).ConfigureAwait(false);
                LogDeadConsumerRemoved(logger, consumer.Name);
            }
        }
    }

    internal static bool IsDeadConsumer(string name, long pending, long idleMilliseconds, string self) =>
        !string.Equals(name, self, StringComparison.Ordinal)
        && pending == 0
        && idleMilliseconds >= DeadConsumerIdle.TotalMilliseconds;

    private async Task RemoveOwnConsumerAsync()
    {
        // Graceful shutdown: don't leave this process's consumer behind. Entries are acked right after
        // dispatch, so nothing meaningful is pending; a crash skips this and maintenance cleans up.
        try
        {
            await redis.GetDatabase().StreamDeleteConsumerAsync(
                RealtimeNotificationChannel.Key,
                RealtimeNotificationChannel.ConsumerGroup,
                _consumerName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogConsumerRemovalFailed(logger, _consumerName, ex);
        }
    }

    internal static Task DispatchAsync(
        RealtimeNotification notification,
        IRealtimeNotificationService target,
        CancellationToken cancellationToken) =>
        notification.Type switch
        {
            RealtimeNotificationType.AuctionClosed =>
                target.NotifyAuctionClosedAsync(notification.AuctionId, notification.WinnerId, cancellationToken),

            RealtimeNotificationType.AuctionExtended when notification.NewEndTime is { } newEndTime =>
                target.NotifyAuctionExtendedAsync(notification.AuctionId, newEndTime, cancellationToken),

            RealtimeNotificationType.BidPlaced when notification is
            { BidId: { } bidId, BidderId: { } bidderId, Amount: { } amount, Currency: { } currency } =>
                target.NotifyBidPlacedAsync(notification.AuctionId, bidId, bidderId, amount, currency, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported or incomplete real-time notification '{notification.Type}'."),
        };

    private static async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        try
        {
            // "$" = only entries added from now on; createStream so the group can exist before the first XADD.
            await db.StreamCreateConsumerGroupAsync(
                RealtimeNotificationChannel.Key,
                RealtimeNotificationChannel.ConsumerGroup,
                StreamPosition.NewMessages,
                createStream: true).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // Group already exists (another replica or a previous run created it).
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Real-time notification relay error. Retrying shortly.")]
    private static partial void LogRelayError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispatch real-time notification stream entry {EntryId}. Skipping.")]
    private static partial void LogDispatchFailed(ILogger logger, string entryId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reclaimed {Count} real-time notification(s) stranded with a dead consumer.")]
    private static partial void LogReclaimedEntries(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed dead real-time relay consumer {Consumer}.")]
    private static partial void LogDeadConsumerRemoved(ILogger logger, string consumer);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove relay consumer {Consumer} on shutdown.")]
    private static partial void LogConsumerRemovalFailed(ILogger logger, string consumer, Exception ex);
}
