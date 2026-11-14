using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.ActivateAuction;
using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.Infrastructure.Workers;

public sealed class AuctionActivationWorkerTests : IDisposable
{
    private readonly IAuctionRepository _repository = Substitute.For<IAuctionRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly AuctionActivationWorkerOptions _options = new() { PollingIntervalSeconds = 10, BatchSize = 50 };
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
    private readonly AuctionActivationWorker _worker;

    public AuctionActivationWorkerTests()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(IAuctionRepository)).Returns(_repository);
        serviceProvider.GetService(typeof(ISender)).Returns(_sender);

        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        _worker = new AuctionActivationWorker(
            scopeFactory,
            Options.Create(_options),
            _clock,
            NullLogger<AuctionActivationWorker>.Instance
        );
    }

    public void Dispose() => _worker.Dispose();

    private Auction BuildAuction() => Auction.Create(
        "Test Auction",
        "Description",
        new Money(100m, "USD"),
        _clock.UtcNow.AddDays(-1),
        new AuctionDuration(TimeSpan.FromHours(1)),
        Guid.NewGuid()
    ).Value;

    [Fact]
    public async Task ActivatePendingAuctionsAsync_QueriesRepositoryWithInjectedClockTime()
    {
        _repository
            .GetPendingAuctionsToActivateAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _worker.ActivatePendingAuctionsAsync(CancellationToken.None);

        await _repository.Received(1).GetPendingAuctionsToActivateAsync(
            _clock.UtcNow,
            _options.BatchSize,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ActivatePendingAuctionsAsync_WithPendingAuctions_SendsActivateCommand()
    {
        var auction = BuildAuction();
        _repository
            .GetPendingAuctionsToActivateAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([auction]);
        _sender
            .Send(Arg.Any<ActivateAuctionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(ActivationOutcome.Activated));

        await _worker.ActivatePendingAuctionsAsync(CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<ActivateAuctionCommand>(c => c.AuctionId == auction.Id),
            Arg.Any<CancellationToken>()
        );
    }
}
