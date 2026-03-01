using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Queries.GetActiveAuctions;

public sealed record GetActiveAuctionsQuery(int Page, int PageSize) : IRequest<Result<PagedResult<AuctionSummaryDto>>>;
