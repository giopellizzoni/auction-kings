using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CloseAuction;
using AuctionKings.Shared.Common;
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

public sealed partial class AuctionCloseWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuctionCloseWorkerOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuctionCloseWorker> _logger;

    public AuctionCloseWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuctionCloseWorkerOptions> options,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuctionCloseWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CloseExpiredAuctionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogUnexpectedLoopError(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    internal async Task CloseExpiredAuctionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var expired = await repository.GetExpiredAuctionsAsync(
            _dateTimeProvider.UtcNow,
            _options.BatchSize,
            cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        LogExpiredAuctions(_logger, expired.Count);

        foreach (var auction in expired)
        {
            try
            {
                var result = await sender.Send(new CloseAuctionCommand(auction.Id), cancellationToken);
                if (result.IsFailure)
                {
                    LogCloseFailed(_logger, auction.Id, result.Error);
                }
                else
                {
                    LogAuctionClosed(_logger, auction.Id);
                }
            }
            catch (Exception ex)
            {
                LogUnexpectedError(_logger, ex, auction.Id);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} expired auction(s) to close.")]
    private static partial void LogExpiredAuctions(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to close auction {Id}: {Error}")]
    private static partial void LogCloseFailed(ILogger logger, Guid id, Error error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auction {Id} closed.")]
    private static partial void LogAuctionClosed(ILogger logger, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error closing auction {Id}. Continuing.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception ex, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error in auction-close worker loop. Continuing after delay.")]
    private static partial void LogUnexpectedLoopError(ILogger logger, Exception ex);
}
