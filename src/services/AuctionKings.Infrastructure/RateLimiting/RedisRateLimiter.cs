using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.RateLimiting;

internal sealed partial class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RedisRateLimiter> _logger;

    // Sliding window using a sorted set.
    // Members are scored by their request timestamp (ms).
    // ZREMRANGEBYSCORE prunes entries outside the window before counting.
    // Returns a two-element array: { isAllowed (1/0), currentCount }.
    private const string SlidingWindowScript = """
        local key       = KEYS[1]
        local now       = tonumber(ARGV[1])
        local window_ms = tonumber(ARGV[2])
        local limit     = tonumber(ARGV[3])
        local seq_key   = key .. ':seq'

        redis.call('ZREMRANGEBYSCORE', key, '-inf', now - window_ms)

        local count = redis.call('ZCARD', key)

        if count < limit then
            local seq = redis.call('INCR', seq_key)
            redis.call('ZADD', key, now, tostring(now) .. ':' .. tostring(seq))
            redis.call('PEXPIRE', key, window_ms)
            redis.call('PEXPIRE', seq_key, window_ms)
            return {1, count + 1}
        else
            return {0, count}
        end
        """;

    public RedisRateLimiter(
        IConnectionMultiplexer multiplexer,
        IOptions<RateLimitOptions> options,
        ILogger<RedisRateLimiter> logger
    )
    {
        _multiplexer = multiplexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RateLimitResult> IsAllowedAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowMs = (long)_options.Window.TotalMilliseconds;

            var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
                SlidingWindowScript,
                keys: [new RedisKey(key)],
                values:
                [
                    new RedisValue(now.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new RedisValue(windowMs.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new RedisValue(_options.MaxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ]
            );

            var isAllowed = result is not null && (long)result[0] == 1;
            var count = result is not null ? (int)(long)result[1] : 0;

            return new RateLimitResult(isAllowed, count);
        }
        catch (Exception ex)
        {
            LogRateLimiterError(_logger, ex, key);
            return new RateLimitResult(IsAllowed: true, CurrentCount: 0, IsError: true);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limiter Redis error for key {Key}. Failing open.")]
    private static partial void LogRateLimiterError(ILogger logger, Exception ex, string key);
}
