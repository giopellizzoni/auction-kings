using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CloseAuction;

/// <param name="AuctionId">Auction to close.</param>
/// <param name="Force">
/// Close even if the auction has not reached its end time (administrative early close).
/// The scheduled close worker must never set this.
/// </param>
public sealed record CloseAuctionCommand(Guid AuctionId, bool Force = false) : IRequest<Result>;
