using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.ActivateAuction;

public sealed record ActivateAuctionCommand(Guid AuctionId) : IRequest<Result>;
