using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CreateAuction;

public sealed record CreateAuctionCommand(
    string Title,
    string Description,
    decimal StartingPrice,
    string Currency,
    TimeSpan Duration,
    Guid SellerId,
    DateTime? StartTime = null) : IRequest<Result<Guid>>;
