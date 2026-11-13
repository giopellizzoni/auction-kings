using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using Mapster;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionDetails;

public sealed class GetAuctionDetailsQueryHandler
    : IRequestHandler<GetAuctionDetailsQuery, Result<AuctionDetailDto>>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    private readonly IAuctionRepository _auctionRepository;
    private readonly ICacheService _cacheService;

    public GetAuctionDetailsQueryHandler(
        IAuctionRepository auctionRepository,
        ICacheService cacheService)
    {
        _auctionRepository = auctionRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<AuctionDetailDto>> Handle(
        GetAuctionDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"auction:{request.AuctionId}";

        // An unknown auction is not cached (the factory returns null), so a missing ID is never remembered.
        var dto = await _cacheService.GetOrSetIfFoundAsync(
            CacheScopes.Auction(request.AuctionId),
            cacheKey,
            async ct =>
            {
                var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, ct);
                return auction?.Adapt<AuctionDetailDto>();
            },
            CacheExpiry,
            cancellationToken);

        return dto is null
            ? Result.Failure<AuctionDetailDto>(DomainErrors.Auction.NotFound)
            : Result.Success(dto);
    }
}
