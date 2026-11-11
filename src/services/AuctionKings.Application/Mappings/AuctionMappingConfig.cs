using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Aggregates;
using Mapster;

namespace AuctionKings.Application.Mappings;

internal sealed class AuctionMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Bid, BidDto>()
            .ConstructUsing(src => new BidDto(
                src.Id,
                src.BidderId,
                src.Amount.Amount,
                src.Amount.Currency,
                src.PlacedAt,
                src.Status));

        config.NewConfig<Auction, AuctionSummaryDto>()
            .ConstructUsing(src => new AuctionSummaryDto(
                src.Id,
                src.Title,
                src.CurrentPrice.Amount,
                src.CurrentPrice.Currency,
                src.EndTime,
                src.Status,
                src.Bids.Count));

        config.NewConfig<Auction, AuctionDetailDto>()
            .ConstructUsing(src => new AuctionDetailDto(
                src.Id,
                src.Title,
                src.Description,
                src.CurrentPrice.Amount,
                src.CurrentPrice.Currency,
                src.StartTime,
                src.EndTime,
                src.Status,
                src.Bids.Count));
    }
}
