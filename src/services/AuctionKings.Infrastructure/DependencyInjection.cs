using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Infrastructure.RateLimiting;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found."
            );

        services.AddDbContext<AuctionDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
                .UseSnakeCaseNamingConvention()
        );

        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<IBidRepository, BidRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName)
        );

        var redisConnectionString =
            configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
            redisConfig.AllowAdmin = true;
            return ConnectionMultiplexer.Connect(redisConfig);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        return services;
    }
}
