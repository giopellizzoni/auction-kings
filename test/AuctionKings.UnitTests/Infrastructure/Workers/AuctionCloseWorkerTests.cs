using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CloseAuction;
using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure.Workers;

public sealed class AuctionCloseWorkerTests : IDisposable
{
    private readonly IAuctionRepository _repository = Substitute.For<IAuctionRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly AuctionCloseWorkerOptions _options = new() { PollingIntervalSeconds = 10, BatchSize = 50 };
    private readonly AuctionCloseWorker _worker;

    public AuctionCloseWorkerTests()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(IAuctionRepository)).Returns(_repository);
        serviceProvider.GetService(typeof(ISender)).Returns(_sender);

        scope.ServiceProvider.Returns(serviceProvider);
        var asyncScope = new AsyncServiceScope(scope);
        scopeFactory.CreateAsyncScope().Returns(asyncScope);

        _worker = new AuctionCloseWorker(
            scopeFactory,
            Options.Create(_options),
            NullLogger<AuctionCloseWorker>.Instance
        );
    }

    public void Dispose() => _worker.Dispose();

    private static Auction BuildAuction() => Auction.Create(
        "Test Auction",
        "Description",
        new Money(100m, "USD"),
        DateTime.UtcNow.AddDays(-2),
        new AuctionDuration(TimeSpan.FromHours(1)),
        Guid.NewGuid()
    ).Value;

    [Fact]
    public async Task CloseExpiredAuctionsAsync_WithExpiredAuctions_SendsCloseCommand()
    {
        var auction = BuildAuction();
        _repository
            .GetExpiredAuctionsAsync(Arg.Any<DateTime>(), _options.BatchSize, Arg.Any<CancellationToken>())
            .Returns([auction]);
        _sender
            .Send(Arg.Any<CloseAuctionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _worker.CloseExpiredAuctionsAsync(CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CloseAuctionCommand>(c => c.AuctionId == auction.Id),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CloseExpiredAuctionsAsync_NoExpiredAuctions_SendsNoCommands()
    {
        _repository
            .GetExpiredAuctionsAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _worker.CloseExpiredAuctionsAsync(CancellationToken.None);

        await _sender.DidNotReceive().Send(Arg.Any<CloseAuctionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseExpiredAuctionsAsync_OneAuctionFails_ContinuesWithOthers()
    {
        var a1 = BuildAuction();
        var a2 = BuildAuction();
        _repository
            .GetExpiredAuctionsAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([a1, a2]);
        _sender
            .Send(Arg.Is<CloseAuctionCommand>(c => c.AuctionId == a1.Id), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("transient error"));
        _sender
            .Send(Arg.Is<CloseAuctionCommand>(c => c.AuctionId == a2.Id), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _worker.CloseExpiredAuctionsAsync(CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CloseAuctionCommand>(c => c.AuctionId == a2.Id),
            Arg.Any<CancellationToken>()
        );
    }
}
