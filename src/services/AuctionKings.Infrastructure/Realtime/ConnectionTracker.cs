using AuctionKings.Application.Abstractions;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Realtime;

internal sealed class ConnectionTracker(IConnectionMultiplexer redis) : IConnectionTracker
{
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);

    private static string Key(Guid userId) => $"connections:{userId}";

    public async Task TrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var key = Key(userId);
        await db.SetAddAsync(key, connectionId).ConfigureAwait(false);
        await db.KeyExpireAsync(key, KeyTtl).ConfigureAwait(false);
    }

    public async Task UntrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var key = Key(userId);
        await db.SetRemoveAsync(key, connectionId).ConfigureAwait(false);
        var remaining = await db.SetLengthAsync(key).ConfigureAwait(false);
        if (remaining > 0)
        {
            await db.KeyExpireAsync(key, KeyTtl).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var members = await db.SetMembersAsync(Key(userId)).ConfigureAwait(false);
        return members.Select(static m => m.ToString()).ToArray();
    }
}
