using AuctionKings.Application.Abstractions;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.PlaceBid;

public sealed record PlaceBidCommand(
    Guid AuctionId,
    Guid BidderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey
) : IRequest<Result<Guid>>, IIdempotentCommand;
