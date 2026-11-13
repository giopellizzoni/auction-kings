using AuctionKings.Domain.Events;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.ActivateAuction;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class ActivateAuctionCommandHandlerTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ICacheInvalidationService _cacheInvalidation =
        Substitute.For<ICacheInvalidationService>();
    private readonly ActivateAuctionCommandHandler _handler;

    public ActivateAuctionCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new ActivateAuctionCommandHandler(
            _auctionRepository,
            _unitOfWork,
            _dateTimeProvider,
            _cacheInvalidation
        );
    }

    private static Auction CreatePendingAuction()
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            new Money(100m, "USD"),
            Now.AddHours(1),
            AuctionDuration.FromDays(1),
            Guid.NewGuid()
        ).Value;
        auction.ClearDomainEvents();
        return auction;
    }

    [Fact]
    public async Task ActivateAuctionCommandHandler_PendingAuctionThatAlreadyEnded_ShouldCancelItAndSucceed()
    {
        var auction = CreatePendingAuction();
        _dateTimeProvider.UtcNow.Returns(auction.EndTime.AddMinutes(1));
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var result = await _handler.Handle(new ActivateAuctionCommand(auction.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
        var raised = Assert.IsType<AuctionCancelledEvent>(Assert.Single(auction.GetDomainEvents()));
        Assert.Equal(_dateTimeProvider.UtcNow, raised.CancelledAt);
        await _auctionRepository.Received(1).UpdateAsync(auction, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateAuctionCommandHandler_PendingAuctionInItsWindow_ShouldActivate()
    {
        var auction = CreatePendingAuction();
        _dateTimeProvider.UtcNow.Returns(auction.StartTime.AddMinutes(1));
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var result = await _handler.Handle(new ActivateAuctionCommand(auction.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Active, auction.Status);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateAuctionCommandHandler_UnknownAuction_ShouldFailWithNotFound()
    {
        var id = Guid.NewGuid();
        _auctionRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Auction?)null);

        var result = await _handler.Handle(new ActivateAuctionCommand(id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.NotFound, result.Error);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
