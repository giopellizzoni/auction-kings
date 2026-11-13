using AuctionKings.Application.Abstractions;

namespace AuctionKings.UnitTests.Helpers;

/// <summary>
/// In-memory <see cref="ICacheService"/> for handler tests. It honours scopes the way the Redis
/// implementation does: invalidating a scope makes every entry in it a miss.
/// </summary>
internal sealed class InMemoryScopedCache : ICacheService
{
    private readonly Dictionary<string, object> _plain = [];
    private readonly Dictionary<(string Scope, string Key), object> _scoped = [];
    private readonly List<string> _invalidated = [];

    /// <summary>How many times a factory had to run, i.e. how many scoped lookups missed.</summary>
    public int FactoryCalls { get; private set; }

    public IReadOnlyList<string> InvalidatedScopes => _invalidated;

    public void Seed<T>(string scope, string key, T value)
        where T : class => _scoped[(scope, key)] = value;

    public bool Contains(string scope, string key) => _scoped.ContainsKey((scope, key));

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class => Task.FromResult(_plain.TryGetValue(key, out var value) ? (T?)value : null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        where T : class
    {
        _plain[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _plain.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
        where T : class =>
        await GetOrSetIfFoundAsync<T>(scope, key, async ct => await factory(ct), expiry, cancellationToken)
        ?? throw new InvalidOperationException("The cache factory returned null.");

    public async Task<T?> GetOrSetIfFoundAsync<T>(
        string scope,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (_scoped.TryGetValue((scope, key), out var cached))
        {
            return (T)cached;
        }

        FactoryCalls++;
        var value = await factory(cancellationToken);
        if (value is not null)
        {
            _scoped[(scope, key)] = value;
        }

        return value;
    }

    public Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        _invalidated.Add(scope);
        foreach (var entry in _scoped.Keys.Where(k => k.Scope == scope).ToList())
        {
            _scoped.Remove(entry);
        }

        return Task.CompletedTask;
    }
}
