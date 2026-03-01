using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionBids;

public sealed record GetAuctionBidsQuery(Guid AuctionId, int Page, int PageSize) : IRequest<Result<PagedResult<BidDto>>>;
