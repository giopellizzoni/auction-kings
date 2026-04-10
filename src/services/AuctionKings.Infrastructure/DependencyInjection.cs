using Amazon.SQS;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure.Cache;
using AuctionKings.Infrastructure.HealthChecks;
using AuctionKings.Infrastructure.Messaging;
using AuctionKings.Infrastructure.Outbox;
using AuctionKings.Infrastructure.Persistence;
using AuctionKings.Infrastructure.RateLimiting;
using AuctionKings.Infrastructure.Realtime;
using AuctionKings.Infrastructure.Repositories;
using AuctionKings.Infrastructure.Services;
using AuctionKings.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        services.AddDbContextFactory<AuctionDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
                .UseSnakeCaseNamingConvention(),
            ServiceLifetime.Scoped
        );

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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
            return ConnectionMultiplexer.Connect(redisConfig);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<IConnectionTracker, ConnectionTracker>();

        // Health check backing services (singleton — use IServiceScopeFactory internally for scoped DB/context access)
        services.AddSingleton<IDatabasePingService, DatabasePingService>();
        services.AddSingleton<IOutboxHealthService, OutboxHealthService>();

        services.AddScoped<IDataCleanupService, DataCleanupService>();

        services.Configure<SqsOptions>(configuration.GetSection("AWS:SQS"));
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
            var config = new AmazonSQSConfig { ServiceURL = opts.ServiceURL };
            return new AmazonSQSClient(config);
        });
        services.AddScoped<IEventBus, SqsMessagePublisher>();

        return services;
    }
}
