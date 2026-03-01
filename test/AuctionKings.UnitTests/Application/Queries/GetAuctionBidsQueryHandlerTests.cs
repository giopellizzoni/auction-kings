using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionBids;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Queries;

public sealed class GetAuctionBidsQueryHandlerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = Guid.NewGuid();

    private readonly IBidRepository _bidRepository = Substitute.For<IBidRepository>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly GetAuctionBidsQueryHandler _handler;

    public GetAuctionBidsQueryHandlerTests()
    {
        _cacheService
            .GetAsync<PagedResult<BidDto>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PagedResult<BidDto>?)null);

        _handler = new GetAuctionBidsQueryHandler(_bidRepository, _cacheService);
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
        );

        auction.Activate(Now);
        return auction;
    }

    [Fact]
    public async Task GetAuctionBidsQueryHandler_ShouldReturnBidsOrderedByAmount()
    {
        var auction = CreateActiveAuction();
        auction.PlaceBid(Guid.NewGuid(), new Money(110m, "USD"), Now);
        auction.PlaceBid(Guid.NewGuid(), new Money(150m, "USD"), Now);
        auction.PlaceBid(Guid.NewGuid(), new Money(200m, "USD"), Now);

        // Repository returns bids pre-sorted descending (as it would in production)
        var sortedBids = auction.Bids.OrderByDescending(b => b.Amount.Amount).ToList();
        _bidRepository
            .GetByAuctionIdPagedAsync(auction.Id, 1, 10, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Bid>)sortedBids, sortedBids.Count));

        var query = new GetAuctionBidsQuery(auction.Id, Page: 1, PageSize: 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);

        var items = result.Value.Items.ToList();
        Assert.Equal(200m, items[0].Amount);
        Assert.Equal(150m, items[1].Amount);
        Assert.Equal(110m, items[2].Amount);
    }

    [Fact]
    public async Task GetAuctionBidsQueryHandler_NoBids_ShouldReturnEmptyPage()
    {
        _bidRepository
            .GetByAuctionIdPagedAsync(Arg.Any<Guid>(), 1, 10, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Bid>)[], 0));

        var query = new GetAuctionBidsQuery(Guid.NewGuid(), Page: 1, PageSize: 10);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }
}
