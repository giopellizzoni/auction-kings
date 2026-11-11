using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuctionKings.Infrastructure.Services;

internal sealed partial class IdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    /// <summary>
    /// A request holding a reservation longer than this is presumed dead. Must comfortably exceed the
    /// slowest legitimate PlaceBid execution (3 optimistic-concurrency attempts + DB retries).
    /// </summary>
    internal static readonly TimeSpan StaleReservationAge = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<AuctionDbContext> _contextFactory;
    private readonly ICacheService _cache;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IDbContextFactory<AuctionDbContext> contextFactory,
        ICacheService cache,
        ILogger<IdempotencyService> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetCachedResponseAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _cache.GetAsync<string>(CacheKey(key), cancellationToken);
    }

    public async Task<IdempotencyReservation> TryReserveAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var expires = now.Add(Ttl);

        // Atomic INSERT — ON CONFLICT DO NOTHING returns 0 rows when the key already exists.
        var rowsInserted = await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO idempotency_keys (key, status, response, created_at, expires_at)
            VALUES ({key}, {IdempotencyKey.StatusReserved}, NULL, {now}, {expires})
            ON CONFLICT (key) DO NOTHING
            """,
            cancellationToken);

        if (rowsInserted == 1)
        {
            return IdempotencyReservation.Reserved;
        }

        // A reservation still "Reserved" after StaleReservationAge was abandoned: the owning request
        // crashed between TryReserveAsync and Complete/Release. Take it over atomically (the
        // created_at predicate ensures only one concurrent retry wins) instead of answering "in-flight"
        // until the cleanup worker's next hourly sweep.
        var staleCutoff = now - StaleReservationAge;
        var rowsTakenOver = await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE idempotency_keys
            SET created_at = {now}, expires_at = {expires}
            WHERE key = {key} AND status = {IdempotencyKey.StatusReserved} AND created_at < {staleCutoff}
            """,
            cancellationToken);

        if (rowsTakenOver == 1)
        {
            LogStaleReservationReclaimed(_logger, key);
            return IdempotencyReservation.Reserved;
        }

        // Conflict — inspect the existing record.
        var existing = await context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

        if (existing?.Status == IdempotencyKey.StatusCompleted && existing.Response is not null)
        {
            return IdempotencyReservation.Completed(existing.Response);
        }

        LogInFlight(_logger, key);
        return IdempotencyReservation.InFlight;
    }

    public async Task CompleteAsync(string key, string responseJson, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE idempotency_keys
            SET status = {IdempotencyKey.StatusCompleted}, response = {responseJson}
            WHERE key = {key}
            """,
            cancellationToken);

        // Write Redis fast-path entry so future duplicates skip the DB entirely.
        try
        {
            await _cache.SetAsync<string>(CacheKey(key), responseJson, Ttl, cancellationToken);
        }
        catch (Exception ex)
        {
            LogCacheWriteFailed(_logger, key, ex);
        }
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM idempotency_keys WHERE key = {key}",
            cancellationToken);
    }

    private static string CacheKey(string key) => $"idempotency:{key}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency key '{Key}' is currently in-flight — returning 409.")]
    private static partial void LogInFlight(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency key '{Key}' had an abandoned reservation — reclaimed for this request.")]
    private static partial void LogStaleReservationReclaimed(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write idempotency fast-path cache for key '{Key}'. DB replay will still work.")]
    private static partial void LogCacheWriteFailed(ILogger logger, string key, Exception exception);
}
