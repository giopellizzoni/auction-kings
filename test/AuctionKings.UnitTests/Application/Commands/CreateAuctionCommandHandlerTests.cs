using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Commands.CreateAuction;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class CreateAuctionCommandHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ICacheInvalidationService _cacheInvalidation =
        Substitute.For<ICacheInvalidationService>();
    private readonly CreateAuctionCommandHandler _handler;

    public CreateAuctionCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _handler = new CreateAuctionCommandHandler(
            _auctionRepository,
            _unitOfWork,
            _dateTimeProvider,
            _cacheInvalidation
        );
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
            SellerId: Guid.NewGuid()
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        await _auctionRepository
            .Received(1)
            .AddAsync(Arg.Any<Auction>(), Arg.Any<CancellationToken>());
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
            SellerId: Guid.NewGuid()
        );

        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(
            validationResult.Errors,
            e => e.PropertyName == nameof(CreateAuctionCommand.Title)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAuctionCommandHandler_WhenTheAggregateRejectsTheTitle_ShouldReturnTheErrorAndPersistNothing(
        string title)
    {
        // Called directly, bypassing the validation behaviour: the domain must still answer with a Result.
        var command = new CreateAuctionCommand(
            Title: title,
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromDays(1),
            SellerId: Guid.NewGuid()
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.TitleRequired, result.Error);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task CreateAuctionCommandHandler_WhenTheAggregateRejectsTheSeller_ShouldReturnTheErrorAndPersistNothing()
    {
        var command = new CreateAuctionCommand(
            Title: "Test Auction",
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromDays(1),
            SellerId: Guid.Empty
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Auction.SellerRequired, result.Error);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task CreateAuctionCommandHandler_WithAFutureStartTime_ShouldSucceedAndSaveThePendingAuction()
    {
        // Activate is a no-op before the start time. Checking its Result must not turn that into a failure.
        Auction? saved = null;
        await _auctionRepository.AddAsync(Arg.Do<Auction>(a => saved = a), Arg.Any<CancellationToken>());
        var command = new CreateAuctionCommand(
            Title: "Test Auction",
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromDays(1),
            SellerId: Guid.NewGuid(),
            StartTime: DateTime.UtcNow.AddDays(1)
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(AuctionStatus.Pending, saved.Status);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAuctionCommandHandler_WithStartTimeJustInThePastAndMinimumDuration_ShouldSucceedAndActivate()
    {
        // The validator accepts StartTime >= now - 1 minute and Duration >= 5 minutes, so EndTime > now always holds
        // and the end-time check in Activate cannot reject a valid creation.
        Auction? saved = null;
        await _auctionRepository.AddAsync(Arg.Do<Auction>(a => saved = a), Arg.Any<CancellationToken>());
        var command = new CreateAuctionCommand(
            Title: "Test Auction",
            Description: "A test auction description",
            StartingPrice: 100m,
            Currency: "USD",
            Duration: TimeSpan.FromMinutes(5),
            SellerId: Guid.NewGuid(),
            StartTime: Now.AddSeconds(-59)
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(AuctionStatus.Active, saved.Status);
        Assert.True(saved.EndTime > Now);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private async Task AssertNothingPersistedAsync()
    {
        await _auctionRepository.DidNotReceive().AddAsync(Arg.Any<Auction>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _cacheInvalidation.DidNotReceive().InvalidateActiveListingsAsync(Arg.Any<CancellationToken>());
    }
}
