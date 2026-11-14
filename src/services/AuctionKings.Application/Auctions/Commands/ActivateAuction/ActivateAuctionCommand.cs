using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.ActivateAuction;

public sealed record ActivateAuctionCommand(Guid AuctionId) : IRequest<Result<ActivationOutcome>>;

/// <summary>What an <see cref="ActivateAuctionCommand"/> ended up doing to the auction.</summary>
public enum ActivationOutcome
{
    /// <summary>The auction is Active (newly activated, already active, or not yet due).</summary>
    Activated,

    /// <summary>The auction had already ended without ever being activated, so it was cancelled.</summary>
    Cancelled,
}
