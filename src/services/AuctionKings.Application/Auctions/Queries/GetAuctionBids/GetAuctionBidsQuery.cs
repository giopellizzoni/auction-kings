using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetAuctionBids;

// RequesterId is the authenticated caller; it is used only to flag their own bids.
public sealed record GetAuctionBidsQuery(Guid AuctionId, int Page, int PageSize, Guid RequesterId)
    : IRequest<Result<PagedResult<AuctionBidDto>>>;
