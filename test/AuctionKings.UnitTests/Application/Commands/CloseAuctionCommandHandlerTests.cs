using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CloseAuction;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class CloseAuctionCommandHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IRealtimeNotificationService _notificationService = Substitute.For<IRealtimeNotificationService>();
    private readonly CloseAuctionCommandHandler _handler;

    public CloseAuctionCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new CloseAuctionCommandHandler(
            _auctionRepository,
            _unitOfWork,
            _dateTimeProvider,
            _notificationService);
    }

    private static Auction CreateActiveAuction()
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            new Money(100m, "USD"),
            Now,
            AuctionDuration.FromDays(1),
            SellerId);

        auction.Activate(Now);
        return auction;
    }

    [Fact]
    public async Task CloseAuctionCommandHandler_ActiveAuction_ShouldClose()
    {
        var auction = CreateActiveAuction();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var command = new CloseAuctionCommand(auction.Id);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Closed, auction.Status);
        await _auctionRepository.Received(1).UpdateAsync(auction, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseAuctionCommandHandler_AlreadyClosed_ShouldReturnFailure()
    {
        var auction = CreateActiveAuction();
        auction.Close(Now);
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var command = new CloseAuctionCommand(auction.Id);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.AlreadyClosed", result.Error.Code);
    }
}
