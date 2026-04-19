using System.Text.Json;
using System.Text.Json.Serialization;
using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Cache;

internal sealed partial class RedisCacheService : ICacheService
{
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

    public async Task RemoveByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var server = _multiplexer.GetServer(_multiplexer.GetEndPoints().First());
            var keys = server.KeysAsync(pattern: $"{prefix}*");

            var keyList = new List<RedisKey>();
            await foreach (var key in keys)
            {
                keyList.Add(key);
            }

            if (keyList.Count == 0)
            {
                return;
            }

            var db = _multiplexer.GetDatabase();
            await db.KeyDeleteAsync([.. keyList]);
        }
        catch (Exception ex)
        {
            LogScanDelFailed(_logger, ex, prefix);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis GET failed for key {Key}. Returning null.")]
    private static partial void LogGetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis SET failed for key {Key}. Bypassing cache.")]
    private static partial void LogSetFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis DEL failed for key {Key}. Bypassing cache.")]
    private static partial void LogDelFailed(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis SCAN/DEL failed for prefix {Prefix}. Bypassing cache.")]
    private static partial void LogScanDelFailed(ILogger logger, Exception ex, string prefix);
}
