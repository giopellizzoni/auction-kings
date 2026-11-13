using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using Mapster;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionBids;

public sealed class GetAuctionBidsQueryHandler
    : IRequestHandler<GetAuctionBidsQuery, Result<PagedResult<AuctionBidDto>>>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(2);

    private readonly IBidRepository _bidRepository;
    private readonly ICacheService _cacheService;

    public GetAuctionBidsQueryHandler(IBidRepository bidRepository, ICacheService cacheService)
    {
        _bidRepository = bidRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<PagedResult<AuctionBidDto>>> Handle(
        GetAuctionBidsQuery request,
        CancellationToken cancellationToken)
    {
        // The cache is shared across callers, so it holds the full page (with BidderId) and is
        // never returned directly; every response is projected per caller below.
        var cacheKey = $"auction:{request.AuctionId}:bids:{request.Page}:{request.PageSize}";

        var page = await _cacheService.GetOrSetAsync(
            CacheScopes.Auction(request.AuctionId),
            cacheKey,
            async ct =>
            {
                var (bids, totalCount) = await _bidRepository.GetByAuctionIdPagedAsync(
                    request.AuctionId,
                    request.Page,
                    request.PageSize,
                    ct);

                return new PagedResult<BidDto>(
                    bids.Adapt<List<BidDto>>(),
                    totalCount,
                    request.Page,
                    request.PageSize);
            },
            CacheExpiry,
            cancellationToken);

        var items = page.Items
            .Select(b => new AuctionBidDto(
                b.Id,
                b.Amount,
                b.Currency,
                b.PlacedAt,
                b.Status,
                IsOwnBid: b.BidderId == request.RequesterId))
            .ToList();

        return Result.Success(new PagedResult<AuctionBidDto>(items, page.TotalCount, page.Page, page.PageSize));
    }
}
