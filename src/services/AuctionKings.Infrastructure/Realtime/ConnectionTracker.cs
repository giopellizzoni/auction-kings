using System.Globalization;
using AuctionKings.Application.Abstractions;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Realtime;

internal sealed class ConnectionTracker(IConnectionMultiplexer redis) : IConnectionTracker
{
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);

    // Each script runs atomically on the Redis server, so a key can never exist without its TTL
    // (previously SADD and EXPIRE were separate commands, and a crash between them left the set
    // behind forever). One key per script, so these are also safe on Redis Cluster.

    // KEYS[1] = set key, ARGV[1] = connection id, ARGV[2] = TTL in milliseconds.
    private const string TrackScript = """
        redis.call('SADD', KEYS[1], ARGV[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[2])
        return 1
        """;

    // Same arguments as TrackScript. Redis deletes a set when its last member is removed, so the
    // TTL is only refreshed while other connections remain.
    private const string UntrackScript = """
        redis.call('SREM', KEYS[1], ARGV[1])
        if redis.call('SCARD', KEYS[1]) > 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return 1
        """;

    private static readonly string KeyTtlMilliseconds =
        ((long)KeyTtl.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    private static string Key(Guid userId) => $"connections:{userId}";

    public async Task TrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        await db.ScriptEvaluateAsync(
                TrackScript,
                keys: [new RedisKey(Key(userId))],
                values: [new RedisValue(connectionId), new RedisValue(KeyTtlMilliseconds)])
            .ConfigureAwait(false);
    }

    public async Task UntrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        await db.ScriptEvaluateAsync(
                UntrackScript,
                keys: [new RedisKey(Key(userId))],
                values: [new RedisValue(connectionId), new RedisValue(KeyTtlMilliseconds)])
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var members = await db.SetMembersAsync(Key(userId)).ConfigureAwait(false);
        return members.Select(static m => m.ToString()).ToArray();
    }
}
