using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Users.Queries.GetUserBidHistory;

public sealed record GetUserBidHistoryQuery(Guid UserId, int Page, int PageSize)
    : IRequest<Result<PagedResult<BidDto>>>;
