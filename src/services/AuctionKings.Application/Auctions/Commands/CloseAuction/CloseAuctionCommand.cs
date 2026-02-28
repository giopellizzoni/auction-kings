using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CloseAuction;

public sealed record CloseAuctionCommand(Guid AuctionId) : IRequest<Result>;
