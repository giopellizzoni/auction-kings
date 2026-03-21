using AuctionKings.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.RateLimiting;

internal sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RedisRateLimiter> _logger;

    // Sliding window using a sorted set.
    // Members are scored by their request timestamp (ms).
    // ZREMRANGEBYSCORE prunes entries outside the window before counting.
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
            return 1
        else
            return 0
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

    public async Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowMs = (long)_options.Window.TotalMilliseconds;

            var result = await db.ScriptEvaluateAsync(
                SlidingWindowScript,
                keys: [new RedisKey(key)],
                values:
                [
                    new RedisValue(now.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new RedisValue(windowMs.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new RedisValue(_options.MaxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ]
            );

            return (long)result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Rate limiter Redis error for key {Key}. Failing open.",
                key
            );
            return true;
        }
    }
}
