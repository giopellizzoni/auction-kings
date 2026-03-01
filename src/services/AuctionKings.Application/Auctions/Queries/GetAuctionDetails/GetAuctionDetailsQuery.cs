using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionDetails;

public sealed record GetAuctionDetailsQuery(Guid AuctionId) : IRequest<Result<AuctionDetailDto>>;
