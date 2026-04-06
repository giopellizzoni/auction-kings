using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CloseAuction;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers.Workers;

public sealed class AuctionCloseWorkerOptions
{
    public const string SectionName = "AuctionCloseWorker";
    public int PollingIntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 50;
}

public sealed class AuctionCloseWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuctionCloseWorkerOptions _options;
    private readonly ILogger<AuctionCloseWorker> _logger;

    public AuctionCloseWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuctionCloseWorkerOptions> options,
        ILogger<AuctionCloseWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CloseExpiredAuctionsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    internal async Task CloseExpiredAuctionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var expired = await repository.GetExpiredAuctionsAsync(
            DateTime.UtcNow,
            _options.BatchSize,
            cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} expired auction(s) to close.", expired.Count);

        foreach (var auction in expired)
        {
            try
            {
                var result = await sender.Send(new CloseAuctionCommand(auction.Id), cancellationToken);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to close auction {Id}: {Error}", auction.Id, result.Error);
                }
                else
                {
                    _logger.LogInformation("Auction {Id} closed.", auction.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error closing auction {Id}. Continuing.", auction.Id);
            }
        }
    }
}
