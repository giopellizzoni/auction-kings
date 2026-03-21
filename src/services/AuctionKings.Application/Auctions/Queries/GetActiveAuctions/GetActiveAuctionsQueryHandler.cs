using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using Mapster;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetActiveAuctions;

public sealed class GetActiveAuctionsQueryHandler
    : IRequestHandler<GetActiveAuctionsQuery, Result<PagedResult<AuctionSummaryDto>>>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(1);

    private readonly IAuctionRepository _auctionRepository;
    private readonly ICacheService _cacheService;

    public GetActiveAuctionsQueryHandler(
        IAuctionRepository auctionRepository,
        ICacheService cacheService)
    {
        _auctionRepository = auctionRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<PagedResult<AuctionSummaryDto>>> Handle(
        GetActiveAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"auctions:active:{request.Page}:{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<AuctionSummaryDto>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var auctions = await _auctionRepository.GetActiveAuctionsAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await _auctionRepository.CountActiveAuctionsAsync(cancellationToken);

        var items = auctions.Adapt<List<AuctionSummaryDto>>();
        var result = new PagedResult<AuctionSummaryDto>(items, totalCount, request.Page, request.PageSize);

        await _cacheService.SetAsync(cacheKey, result, CacheExpiry, cancellationToken);

        return Result.Success(result);
    }
}
