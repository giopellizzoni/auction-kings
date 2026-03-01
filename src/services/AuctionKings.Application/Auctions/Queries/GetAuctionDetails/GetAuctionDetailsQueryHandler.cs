using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionDetails;

public sealed class GetAuctionDetailsQueryHandler
    : IRequestHandler<GetAuctionDetailsQuery, Result<AuctionDetailDto>>
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private const int TopBidsCount = 10;

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

        var cached = await _cacheService.GetAsync<AuctionDetailDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction is null)
        {
            return Result.Failure<AuctionDetailDto>(DomainErrors.Auction.NotFound);
        }

        var dto = MapToDto(auction);

        await _cacheService.SetAsync(cacheKey, dto, CacheExpiry, cancellationToken);

        return Result.Success(dto);
    }

    private static AuctionDetailDto MapToDto(Auction auction)
    {
        var topBids = auction.Bids
            .OrderByDescending(b => b.Amount.Amount)
            .Take(TopBidsCount)
            .Select(b => new BidDto(b.Id, b.BidderId, b.Amount.Amount, b.Amount.Currency, b.PlacedAt, b.Status))
            .ToList();

        return new AuctionDetailDto(
            auction.Id,
            auction.Title,
            auction.Description,
            auction.CurrentPrice.Amount,
            auction.CurrentPrice.Currency,
            auction.StartTime,
            auction.EndTime,
            auction.Status,
            auction.Bids.Count,
            topBids);
    }
}
