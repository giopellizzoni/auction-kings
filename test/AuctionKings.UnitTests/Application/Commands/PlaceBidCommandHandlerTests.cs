using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using NSubstitute;

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
    private readonly PlaceBidCommandHandler _handler;

    public PlaceBidCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new PlaceBidCommandHandler(
            _auctionRepository,
            _unitOfWork,
            _dateTimeProvider,
            _notificationService
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

        var command = new PlaceBidCommand(auction.Id, bidderId, 110m, "USD");
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

        var command = new PlaceBidCommand(Guid.NewGuid(), Guid.NewGuid(), 110m, "USD");
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

        var command = new PlaceBidCommand(auction.Id, bidderId, 110m, "USD");
        await _handler.Handle(command, CancellationToken.None);

        await _notificationService
            .Received(1)
            .NotifyBidPlacedAsync(
                auction.Id,
                Arg.Any<Guid>(),
                bidderId,
                110m,
                "USD",
                Arg.Any<CancellationToken>()
            );
    }
}
