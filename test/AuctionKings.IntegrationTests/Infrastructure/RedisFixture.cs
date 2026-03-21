using StackExchange.Redis;
using Testcontainers.Redis;

namespace AuctionKings.IntegrationTests.Infrastructure;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer CreateMultiplexer()
    {
        var config = ConfigurationOptions.Parse(_container.GetConnectionString());
        config.AllowAdmin = true;
        return ConnectionMultiplexer.Connect(config);
    }

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
