using System.Diagnostics;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Realtime;
using AuctionKings.Shared.Common;
using StackExchange.Redis;

namespace AuctionKings.API.Realtime;

internal sealed record RealtimeRelayOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromSeconds(2);
    public int BatchSize { get; init; } = 50;

    /// <summary>How long an entry may sit unacknowledged with a consumer before another replica takes it over.</summary>
    public TimeSpan ClaimIdle { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan ClaimEvery { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Entries older than this are dropped: a push that late is noise, not news.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Pushes the real-time notifications that other processes (the background worker) leave on the Redis Stream
/// to the SignalR hubs. All API replicas share one consumer group, so each entry is delivered by exactly one
/// replica (a plain Pub/Sub subscription would make every replica re-send it through the backplane).
/// Delivery is at least once: an entry is acknowledged only after it was handled, and entries stuck with a
/// crashed replica are taken over by another one. Malformed, unknown or stale entries are logged and dropped.
/// </summary>
internal sealed partial class RealtimeStreamRelay(
    IConnectionMultiplexer redis,
    IRealtimeNotificationService notifications,
    IDateTimeProvider clock,
    RealtimeRelayOptions options,
    ILogger<RealtimeStreamRelay> logger) : BackgroundService
{
    private readonly string _consumer = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly Stopwatch _sinceClaim = Stopwatch.StartNew();
    private bool _groupReady;
    private bool _claimedOnce;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = await ProcessOnceAsync(stoppingToken).ConfigureAwait(false);
                if (handled == 0)
                {
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopFailed(logger, ex);
                _groupReady = false; // e.g. the stream was deleted (NOGROUP): recreate the group next time.
                await DelayAsync(options.ErrorDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>One pass: take over abandoned entries now and then, then read new ones. Returns how many were handled.</summary>
    internal async Task<int> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await EnsureGroupAsync(db).ConfigureAwait(false);

        var handled = 0;

        if (!_claimedOnce || _sinceClaim.Elapsed >= options.ClaimEvery)
        {
            _claimedOnce = true;
            _sinceClaim.Restart();

            var claimed = await db
                .StreamAutoClaimAsync(
                    RealtimeFeed.Key,
                    RealtimeFeed.Group,
                    _consumer,
                    (long)options.ClaimIdle.TotalMilliseconds,
                    "0-0",
                    options.BatchSize)
                .ConfigureAwait(false);

            foreach (var entry in claimed.ClaimedEntries)
            {
                await HandleAsync(db, entry, cancellationToken).ConfigureAwait(false);
                handled++;
            }
        }

        var entries = await db
            .StreamReadGroupAsync(
                RealtimeFeed.Key,
                RealtimeFeed.Group,
                _consumer,
                StreamPosition.NewMessages,
                options.BatchSize)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            await HandleAsync(db, entry, cancellationToken).ConfigureAwait(false);
            handled++;
        }

        return handled;
    }

    private async Task EnsureGroupAsync(IDatabase db)
    {
        if (_groupReady)
        {
            return;
        }

        try
        {
            // From the beginning: entries the worker wrote before the first API replica came up are not lost
            // (stale ones are dropped by age).
            await db
                .StreamCreateConsumerGroupAsync(
                    RealtimeFeed.Key,
                    RealtimeFeed.Group,
                    StreamPosition.Beginning,
                    createStream: true)
                .ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
            // Another replica (or an earlier run) already created it.
        }

        _groupReady = true;
    }

    private async Task HandleAsync(IDatabase db, StreamEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await DispatchAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Shutting down mid-delivery: leave it unacknowledged so another replica takes it over.
        }
        catch (Exception ex)
        {
            // A handler bug must not wedge the stream in an endless retry: log and drop the entry.
            LogDispatchFailed(logger, entry.Id.ToString(), ex);
        }

        await db.StreamAcknowledgeAsync(RealtimeFeed.Key, RealtimeFeed.Group, entry.Id).ConfigureAwait(false);
    }

    private async Task DispatchAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        var id = entry.Id.ToString();
        var envelope = RealtimeFeed.TryParse(entry[RealtimeFeed.Field]);

        if (envelope is null)
        {
            LogMalformed(logger, id);
            return;
        }

        if (envelope.Version != RealtimeFeed.CurrentVersion)
        {
            LogUnsupportedVersion(logger, id, envelope.Version);
            return;
        }

        if (clock.UtcNow - envelope.OccurredAt > options.MaxAge)
        {
            LogStale(logger, id, envelope.Kind);
            return;
        }

        switch (envelope.Kind)
        {
            case RealtimeKinds.BidPlaced when RealtimeFeed.TryReadPayload<BidPlacedPayload>(envelope) is { } p:
                await notifications
                    .NotifyBidPlacedAsync(p.AuctionId, p.BidId, p.Amount, p.Currency, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case RealtimeKinds.AuctionClosed when RealtimeFeed.TryReadPayload<AuctionClosedPayload>(envelope) is { } p:
                await notifications
                    .NotifyAuctionClosedAsync(p.AuctionId, p.WinnerId, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case RealtimeKinds.AuctionExtended when RealtimeFeed.TryReadPayload<AuctionExtendedPayload>(envelope) is { } p:
                await notifications
                    .NotifyAuctionExtendedAsync(p.AuctionId, p.NewEndTime, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case RealtimeKinds.AuctionCancelled when RealtimeFeed.TryReadPayload<AuctionCancelledPayload>(envelope) is { } p:
                await notifications
                    .NotifyAuctionCancelledAsync(p.SellerId, p.AuctionId, p.Title, p.CancelledAt, cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                LogUnknownKind(logger, id, envelope.Kind);
                break;
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The real-time stream relay failed; retrying.")]
    private static partial void LogLoopFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivering real-time stream entry {EntryId} failed; it was dropped.")]
    private static partial void LogDispatchFailed(ILogger logger, string entryId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped malformed real-time stream entry {EntryId}.")]
    private static partial void LogMalformed(ILogger logger, string entryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped real-time stream entry {EntryId}: unsupported version {Version}.")]
    private static partial void LogUnsupportedVersion(ILogger logger, string entryId, int version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dropped stale real-time stream entry {EntryId} ({Kind}).")]
    private static partial void LogStale(ILogger logger, string entryId, string kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped real-time stream entry {EntryId}: unknown or invalid kind '{Kind}'.")]
    private static partial void LogUnknownKind(ILogger logger, string entryId, string kind);
}
