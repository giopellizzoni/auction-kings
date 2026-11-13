namespace AuctionKings.Application.Abstractions;

/// <summary>
/// Cache with two kinds of entries:
/// <list type="bullet">
/// <item>Plain keys (<see cref="GetAsync{T}"/>, <see cref="SetAsync{T}"/>, <see cref="RemoveAsync"/>) for data that
/// is only ever removed by key or by expiry, such as stored idempotency responses.</item>
/// <item>Scoped entries (<see cref="GetOrSetAsync{T}"/>, <see cref="GetOrSetIfFoundAsync{T}"/>) that belong to a
/// scope (see <see cref="CacheScopes"/>) and are all invalidated together by <see cref="InvalidateScopeAsync"/>
/// in constant time, without enumerating keys.</item>
/// </list>
/// All operations fail open: if the cache is unavailable, reads behave as misses and writes are skipped.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default
    )
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the entry for <paramref name="key"/> in <paramref name="scope"/>, or runs
    /// <paramref name="factory"/> and caches its result. Data loaded by the factory is only ever served
    /// if its scope was not invalidated after the lookup started, so a slow read cannot re-cache data that
    /// an invalidation was meant to remove. Invalidate after committing the change.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default
    )
        where T : class;

    /// <summary>
    /// Like <see cref="GetOrSetAsync{T}"/>, but a <see langword="null"/> result from the factory means
    /// "not found": it is returned and not cached.
    /// </summary>
    Task<T?> GetOrSetIfFoundAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default
    )
        where T : class;

    /// <summary>Makes every entry currently cached under <paramref name="scope"/> unreachable.</summary>
    Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default);
}
