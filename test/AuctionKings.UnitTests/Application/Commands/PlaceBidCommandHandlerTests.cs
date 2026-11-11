using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Application.Exceptions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using NSubstitute;
using NSubstitute.Core;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class PlaceBidCommandHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IRealtimeNotificationService _notificationService =
        Substitute.For<IRealtimeNotificationService>();
    private readonly ICacheInvalidationService _cacheInvalidation =
        Substitute.For<ICacheInvalidationService>();
    private readonly PlaceBidCommandHandler _handler;

    public PlaceBidCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new PlaceBidCommandHandler(
            _auctionRepository,
            _unitOfWork,
            _dateTimeProvider,
            _notificationService,
            _cacheInvalidation
        );
    }

    private static Auction CreateActiveAuction(Guid? sellerId = null)
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            new Money(100m, "USD"),
            Now,
            AuctionDuration.FromDays(1),
            sellerId ?? SellerId
        );

        auction.Activate(Now);
        return auction;
    }

    [Fact]
    public async Task PlaceBidCommandHandler_ValidBid_ShouldSucceed()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var command = new PlaceBidCommand(auction.Id, bidderId, 110m, "USD", "test-key");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        await _auctionRepository.Received(1).UpdateAsync(auction, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceBidCommandHandler_AuctionNotFound_ShouldReturnFailure()
    {
        _auctionRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Auction?)null);

        var command = new PlaceBidCommand(Guid.NewGuid(), Guid.NewGuid(), 110m, "USD", "test-key");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task PlaceBidCommandHandler_ShouldNotifyViaRealtime()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var command = new PlaceBidCommand(auction.Id, bidderId, 110m, "USD", "test-key");
        await _handler.Handle(command, CancellationToken.None);

        await _notificationService
            .Received(1)
            .NotifyBidPlacedAsync(
                auction.Id,
                Arg.Any<Guid>(),
                110m,
                "USD",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task PlaceBidCommandHandler_ConcurrencyConflictOnFirstAttempt_ShouldRetryAndSucceed()
    {
        var auctionId = Guid.NewGuid();
        var bidderId = Guid.NewGuid();
        var commitAttempt = 0;

        _auctionRepository
            .GetByIdAsync(auctionId, Arg.Any<CancellationToken>())
            .Returns(_ => CreateActiveAuction());

        _unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns(
                (Func<CallInfo, Task<int>>)(
                    _ =>
                    {
                        if (commitAttempt++ == 0)
                        {
                            throw new ConcurrencyException();
                        }

                        return Task.FromResult(1);
                    }
                )
            );

        var command = new PlaceBidCommand(auctionId, bidderId, 110m, "USD", "test-key");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _auctionRepository.Received(2).GetByIdAsync(auctionId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceBidCommandHandler_AllRetriesExhausted_ShouldReturnConcurrencyConflict()
    {
        var auctionId = Guid.NewGuid();

        _auctionRepository
            .GetByIdAsync(auctionId, Arg.Any<CancellationToken>())
            .Returns(_ => CreateActiveAuction());

        _unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns((Func<CallInfo, Task<int>>)(_ => throw new ConcurrencyException()));

        var command = new PlaceBidCommand(auctionId, Guid.NewGuid(), 110m, "USD", "test-key");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.ConcurrencyConflict", result.Error.Code);
        await _unitOfWork.Received(3).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceBidCommandHandler_ConcurrencyConflict_ShouldClearTrackingBetweenRetries()
    {
        var auctionId = Guid.NewGuid();
        var commitAttempt = 0;

        _auctionRepository
            .GetByIdAsync(auctionId, Arg.Any<CancellationToken>())
            .Returns(_ => CreateActiveAuction());

        _unitOfWork
            .CommitAsync(Arg.Any<CancellationToken>())
            .Returns(
                (Func<CallInfo, Task<int>>)(
                    _ =>
                    {
                        if (commitAttempt++ == 0)
                        {
                            throw new ConcurrencyException();
                        }

                        return Task.FromResult(1);
                    }
                )
            );

        var command = new PlaceBidCommand(auctionId, Guid.NewGuid(), 110m, "USD", "test-key");
        await _handler.Handle(command, CancellationToken.None);

        _unitOfWork.Received(1).ClearTracking();
    }
}
