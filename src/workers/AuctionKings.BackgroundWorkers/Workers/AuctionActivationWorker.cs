using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.ActivateAuction;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuctionKings.BackgroundWorkers.Workers;

public sealed class AuctionActivationWorkerOptions
{
    public const string SectionName = "AuctionActivationWorker";
    public int PollingIntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 50;
}

public sealed partial class AuctionActivationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuctionActivationWorkerOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuctionActivationWorker> _logger;

    public AuctionActivationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuctionActivationWorkerOptions> options,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuctionActivationWorker> logger)
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
                await ActivatePendingAuctionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogUnexpectedLoopError(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    internal async Task ActivatePendingAuctionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var pending = await repository.GetPendingAuctionsToActivateAsync(
            _dateTimeProvider.UtcNow,
            _options.BatchSize,
            cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        LogPendingAuctions(_logger, pending.Count);

        foreach (var auction in pending)
        {
            try
            {
                var result = await sender.Send(new ActivateAuctionCommand(auction.Id), cancellationToken);
                if (result.IsFailure)
                {
                    LogActivateFailed(_logger, auction.Id, result.Error);
                }
                else
                {
                    LogAuctionActivated(_logger, auction.Id);
                }
            }
            catch (Exception ex)
            {
                LogUnexpectedError(_logger, ex, auction.Id);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} pending auction(s) to activate.")]
    private static partial void LogPendingAuctions(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to activate auction {Id}: {Error}")]
    private static partial void LogActivateFailed(ILogger logger, Guid id, Error error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auction {Id} activated.")]
    private static partial void LogAuctionActivated(ILogger logger, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error activating auction {Id}. Continuing.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception ex, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error in activation worker loop. Continuing after delay.")]
    private static partial void LogUnexpectedLoopError(ILogger logger, Exception ex);
}
