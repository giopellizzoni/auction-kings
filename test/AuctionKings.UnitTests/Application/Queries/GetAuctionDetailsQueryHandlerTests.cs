using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.UnitTests.Helpers;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Queries;

[Collection(nameof(MapsterTestFixtures))]
public sealed class GetAuctionDetailsQueryHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly InMemoryScopedCache _cacheService = new();
    private readonly GetAuctionDetailsQueryHandler _handler;

    public GetAuctionDetailsQueryHandlerTests()
    {
        _handler = new GetAuctionDetailsQueryHandler(_auctionRepository, _cacheService);
    }

    private static Auction CreateActiveAuction()
    {
        var auction = Auction.Create(
            "Test Auction",
            "Description",
            new Money(100m, "USD"),
            Now,
            AuctionDuration.FromDays(1),
            SellerId
        ).Value;

        auction.Activate(Now);
        return auction;
    }

    [Fact]
    public async Task GetAuctionDetailsQueryHandler_ExistingAuction_ShouldReturnDto()
    {
        var auction = CreateActiveAuction();
        var bidderId = Guid.NewGuid();
        auction.PlaceBid(bidderId, new Money(150m, "USD"), Now);

        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);

        var query = new GetAuctionDetailsQuery(auction.Id);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(auction.Id, result.Value.Id);
        Assert.Equal(auction.Title, result.Value.Title);
        Assert.Equal(auction.Description, result.Value.Description);
        Assert.Equal(150m, result.Value.CurrentPrice);
        Assert.Equal("USD", result.Value.Currency);
        Assert.Equal(1, result.Value.BidCount);
    }

    [Fact]
    public async Task GetAuctionDetailsQueryHandler_NonExistent_ShouldReturnFailure()
    {
        _auctionRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Auction?)null);

        var query = new GetAuctionDetailsQuery(Guid.NewGuid());
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Auction.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetAuctionDetailsQueryHandler_CachedResult_ShouldNotHitRepository()
    {
        var auction = CreateActiveAuction();
        var cachedDto = new AuctionDetailDto(
            auction.Id,
            auction.Title,
            auction.Description,
            100m,
            "USD",
            Now,
            Now.AddDays(1),
            auction.Status,
            0
        );

        _cacheService.Seed(CacheScopes.Auction(auction.Id), $"auction:{auction.Id}", cachedDto);

        var query = new GetAuctionDetailsQuery(auction.Id);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(cachedDto, result.Value);
        await _auctionRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuctionDetailsQueryHandler_NonExistent_ShouldNotBeCached()
    {
        var id = Guid.NewGuid();
        _auctionRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Auction?)null);

        await _handler.Handle(new GetAuctionDetailsQuery(id), CancellationToken.None);
        await _handler.Handle(new GetAuctionDetailsQuery(id), CancellationToken.None);

        // A missing auction is looked up every time, so it is never remembered after it is created.
        Assert.Equal(2, _cacheService.FactoryCalls);
        Assert.False(_cacheService.Contains(CacheScopes.Auction(id), $"auction:{id}"));
    }

    [Fact]
    public async Task GetAuctionDetailsQueryHandler_ShouldCacheInTheAuctionScope_AndReloadAfterInvalidation()
    {
        var auction = CreateActiveAuction();
        _auctionRepository.GetByIdAsync(auction.Id, Arg.Any<CancellationToken>()).Returns(auction);
        var query = new GetAuctionDetailsQuery(auction.Id);

        await _handler.Handle(query, CancellationToken.None);
        await _handler.Handle(query, CancellationToken.None);
        Assert.Equal(1, _cacheService.FactoryCalls);
        Assert.True(_cacheService.Contains(CacheScopes.Auction(auction.Id), $"auction:{auction.Id}"));

        await _cacheService.InvalidateScopeAsync(CacheScopes.Auction(auction.Id));
        await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, _cacheService.FactoryCalls);
    }
}
