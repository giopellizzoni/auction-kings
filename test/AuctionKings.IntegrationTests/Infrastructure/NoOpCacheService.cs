using AuctionKings.Application.Abstractions;

namespace AuctionKings.IntegrationTests.Infrastructure;

/// <summary>
/// Cache that never hits: reads return null, writes are discarded, and
/// <see cref="GetOrSetAsync{T}"/> always runs the factory. Lets Postgres-only integration
/// tests exercise the database path (e.g. idempotency reservation) without a Redis container.
/// </summary>
public sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class => Task.FromResult<T?>(null);

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default
    )
        where T : class => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default
    )
        where T : class => factory(cancellationToken);
}
