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

        var result = await _cacheService.GetOrSetAsync(
            CacheScopes.ActiveListings,
            cacheKey,
            async ct =>
            {
                var auctions = await _auctionRepository.GetActiveAuctionsAsync(request.Page, request.PageSize, ct);
                var totalCount = await _auctionRepository.CountActiveAuctionsAsync(ct);

                var items = auctions.Adapt<List<AuctionSummaryDto>>();
                return new PagedResult<AuctionSummaryDto>(items, totalCount, request.Page, request.PageSize);
            },
            CacheExpiry,
            cancellationToken);

        return Result.Success(result);
    }
}
