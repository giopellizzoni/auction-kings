using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using Mapster;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionBids;

public sealed class GetAuctionBidsQueryHandler
    : IRequestHandler<GetAuctionBidsQuery, Result<PagedResult<BidDto>>>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(2);

    private readonly IBidRepository _bidRepository;
    private readonly ICacheService _cacheService;

    public GetAuctionBidsQueryHandler(IBidRepository bidRepository, ICacheService cacheService)
    {
        _bidRepository = bidRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<PagedResult<BidDto>>> Handle(
        GetAuctionBidsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"auction:{request.AuctionId}:bids:{request.Page}:{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<BidDto>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var (bids, totalCount) = await _bidRepository.GetByAuctionIdPagedAsync(
            request.AuctionId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var items = bids.Adapt<List<BidDto>>();

        var result = new PagedResult<BidDto>(items, totalCount, request.Page, request.PageSize);

        await _cacheService.SetAsync(cacheKey, result, CacheExpiry, cancellationToken);

        return Result.Success(result);
    }
}
