using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CreateAuction;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Shared.Common;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class CreateAuctionCommandHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly CreateAuctionCommandHandler _handler;

    public CreateAuctionCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new CreateAuctionCommandHandler(_auctionRepository, _unitOfWork, _dateTimeProvider);
    }

    [Fact]
    public async Task CreateAuctionCommandHandler_ValidCommand_ShouldCreateAuction()
    {
        var command = new CreateAuctionCommand(
            Title: "Test Auction",
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromDays(1),
            SellerId: Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        await _auctionRepository.Received(1).AddAsync(Arg.Any<Auction>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAuctionCommandHandler_InvalidTitle_ShouldFailValidation()
    {
        var validator = new CreateAuctionCommandValidator();
        var command = new CreateAuctionCommand(
            Title: "AB",
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromDays(1),
            SellerId: Guid.NewGuid());

        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(CreateAuctionCommand.Title));
    }
}
