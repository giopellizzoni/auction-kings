using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetActiveAuctions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.UnitTests.Helpers;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Queries;

[Collection(nameof(MapsterTestFixtures))]
public sealed class GetActiveAuctionsQueryHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAuctionRepository _auctionRepository = Substitute.For<IAuctionRepository>();
    private readonly InMemoryScopedCache _cacheService = new();
    private readonly GetActiveAuctionsQueryHandler _handler;

    public GetActiveAuctionsQueryHandlerTests()
    {
        _handler = new GetActiveAuctionsQueryHandler(_auctionRepository, _cacheService);
    }

    private static Auction CreateActiveAuction(string title = "Test Auction")
    {
        var auction = Auction.Create(
            title,
            "Description",
            new Money(100m, "USD"),
            Now,
            AuctionDuration.FromDays(1),
            Guid.NewGuid());

        auction.Activate(Now);
        return auction;
    }

    [Fact]
    public async Task GetActiveAuctionsQueryHandler_ShouldReturnPaginatedResults()
    {
        var auctions = new List<Auction>
        {
            CreateActiveAuction("Auction 1"),
            CreateActiveAuction("Auction 2"),
        };

        _auctionRepository.GetActiveAuctionsAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(auctions);
        _auctionRepository.CountActiveAuctionsAsync(Arg.Any<CancellationToken>())
            .Returns(2);

        var query = new GetActiveAuctionsQuery(Page: 1, PageSize: 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
        Assert.All(result.Value.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
    }

    [Fact]
    public async Task GetActiveAuctionsQueryHandler_ShouldCachePagesInTheActiveListingsScope()
    {
        _auctionRepository.GetActiveAuctionsAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new List<Auction> { CreateActiveAuction() });
        _auctionRepository.CountActiveAuctionsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var query = new GetActiveAuctionsQuery(Page: 1, PageSize: 10);
        await _handler.Handle(query, CancellationToken.None);
        await _handler.Handle(query, CancellationToken.None);

        // Second call is a hit, and the entry lives in the scope InvalidateActiveListingsAsync clears.
        Assert.Equal(1, _cacheService.FactoryCalls);
        Assert.True(_cacheService.Contains(CacheScopes.ActiveListings, "auctions:active:1:10"));
    }

    [Fact]
    public async Task GetActiveAuctionsQueryHandler_AfterInvalidatingTheScope_ShouldReloadFromTheRepository()
    {
        _auctionRepository.GetActiveAuctionsAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new List<Auction> { CreateActiveAuction() });
        _auctionRepository.CountActiveAuctionsAsync(Arg.Any<CancellationToken>()).Returns(1);
        var query = new GetActiveAuctionsQuery(Page: 1, PageSize: 10);
        await _handler.Handle(query, CancellationToken.None);

        await _cacheService.InvalidateScopeAsync(CacheScopes.ActiveListings);
        await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, _cacheService.FactoryCalls);
    }

    [Fact]
    public async Task GetActiveAuctionsQueryHandler_NoActiveAuctions_ShouldReturnEmptyPage()
    {
        _auctionRepository.GetActiveAuctionsAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new List<Auction>());
        _auctionRepository.CountActiveAuctionsAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        var query = new GetActiveAuctionsQuery(Page: 1, PageSize: 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }
}
