using System.Text.Json;
using System.Text.Json.Serialization;
using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Cache;

internal sealed partial class RedisCacheService : ICacheService
{
    // Scoped entries are invalidated by versioning, never by scanning for keys.
    //
    // Each scope has a marker key `cachever:{scope}` holding a random version, and every scoped entry is
    // stored as `{version}|{json}`. A read is a hit only if the entry's version equals the scope's current
    // version, so replacing the marker (one SET) makes every earlier entry unreachable. Those entries are
    // not deleted; they simply expire. Only single-key commands are used, so this is safe on Redis Cluster.
    //
    // A missing marker reads as NoVersion. Versions are random GUIDs and never repeat, so a marker that
    // expires and is later recreated can never match an old entry again.

    /// <summary>
    /// Lifetime of a scope's version marker. Entry lifetimes are clamped to this so that no entry outlives
    /// the marker that validates it.
    /// </summary>
    internal static readonly TimeSpan VersionTtl = TimeSpan.FromHours(1);

    private const string NoVersion = "0";
    private const char VersionSeparator = '|';

    internal static string VersionKey(string scope) => $"cachever:{scope}";

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly CacheOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public RedisCacheService(
        IConnectionMultiplexer multiplexer,
        IOptions<CacheOptions> options,
        ILogger<RedisCacheService> logger
    )
    {
        _multiplexer = multiplexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
        }
        catch (Exception ex)
        {
            LogGetFailed(_logger, ex, key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var serialized = JsonSerializer.Serialize(value, SerializerOptions);
            await db.StringSetAsync(key, serialized, expiry ?? _options.DetailsTtl);
        }
        catch (Exception ex)
        {
            LogSetFailed(_logger, ex, key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            LogDelFailed(_logger, ex, key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var value = await GetOrSetCoreAsync<T>(
                scope,
                key,
                async token => await factory(token).ConfigureAwait(false),
                expiry,
                cancellationToken)
            .ConfigureAwait(false);

        return value
            ?? throw new InvalidOperationException(
                "The cache factory returned null. Use GetOrSetIfFoundAsync when null means 'not found'.");
    }

    public Task<T?> GetOrSetIfFoundAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default
    )
        where T : class => GetOrSetCoreAsync(scope, key, factory, expiry, cancellationToken);

    public async Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        try
        {
            var db = _multiplexer.GetDatabase();

            // One atomic SET (value + TTL together). A fresh random version cannot repeat an old one.
            await db.StringSetAsync(VersionKey(scope), Guid.NewGuid().ToString("N"), VersionTtl)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Entries cached under the old version may be served until they expire.
            LogInvalidateFailed(_logger, ex, scope);
        }
    }

    private async Task<T?> GetOrSetCoreAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken
    )
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry, TimeSpan.Zero);

        // Null means the cache could not be read, so nothing is read from or written to it for this call.
        string? version = null;

        try
        {
            var db = _multiplexer.GetDatabase();

            // Sent back to back, so both reads share one round trip.
            var versionRead = db.StringGetAsync(VersionKey(scope));
            var entryRead = db.StringGetAsync(key);
            await Task.WhenAll(versionRead, entryRead).ConfigureAwait(false);

            // Captured before the factory runs. If the scope is invalidated while the factory is loading,
            // the result is stored under this older version and can never be served.
            version = versionRead.Result.HasValue ? versionRead.Result.ToString() : NoVersion;

            if (TryReadEntry<T>(entryRead.Result, version, key, out var cached))
            {
                return cached;
            }
        }
        catch (Exception ex)
        {
            version = null;
            LogGetFailed(_logger, ex, key);
        }

        var value = await factory(cancellationToken).ConfigureAwait(false);

        if (value is not null && version is not null)
        {
            await StoreAsync(key, version, value, expiry).ConfigureAwait(false);
        }

        return value;
    }

    private async Task StoreAsync<T>(string key, string version, T value, TimeSpan expiry)
        where T : class
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var entry = string.Concat(version, VersionSeparator.ToString(), JsonSerializer.Serialize(value, SerializerOptions));

            // Never outlive the version marker that validates the entry.
            var ttl = expiry > VersionTtl ? VersionTtl : expiry;
            await db.StringSetAsync(key, entry, ttl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSetFailed(_logger, ex, key);
        }
    }

    private bool TryReadEntry<T>(RedisValue raw, string expectedVersion, string key, out T? value)
        where T : class
    {
        value = null;

        if (!raw.HasValue)
        {
            return false;
        }

        var text = raw.ToString();
        var separator = text.IndexOf(VersionSeparator, StringComparison.Ordinal);

        // No separator: not a scoped entry. Different version: written before the last invalidation.
        if (separator <= 0 || !text.AsSpan(0, separator).SequenceEqual(expectedVersion))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(text.AsSpan(separator + 1), SerializerOptions);
            return value is not null;
        }
        catch (JsonException ex)
        {
            LogCorruptEntry(_logger, ex, key);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis GET failed for key {Key}. Returning null.")]
    private static partial void LogGetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis SET failed for key {Key}. Bypassing cache.")]
    private static partial void LogSetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis DEL failed for key {Key}. Bypassing cache.")]
    private static partial void LogDelFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cache scope {Scope} could not be invalidated. Entries may be served until they expire.")]
    private static partial void LogInvalidateFailed(ILogger logger, Exception ex, string scope);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache entry {Key} could not be deserialized. Treating it as a miss.")]
    private static partial void LogCorruptEntry(ILogger logger, Exception ex, string key);
}
