using AuctionKings.API.Auth;
using AuctionKings.API.Filters;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Application.Auctions.Queries.GetAuctionBids;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace AuctionKings.API.Controllers;

public sealed record PlaceBidRequest(decimal Amount, string Currency);

[Route("api/v1/auctions/{auctionId:guid}/bids")]
public sealed class BidsController : ApiControllerBase
{
    private static readonly Counter BidsPlaced = Metrics.CreateCounter(
        "auction_kings_bids_placed_total",
        "Total number of bids successfully placed."
    );

    private readonly ISender _sender;

    public BidsController(ISender sender) => _sender = sender;

    [HttpPost]
    [Authorize(Policy = Policies.Bidder)]
    [Idempotent]
    [RateLimit(policy: "place-bid", failOpen: false)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PlaceBid(
        [FromRoute] Guid auctionId,
        [FromBody] PlaceBidRequest request,
        CancellationToken cancellationToken
    )
    {
        var bidderId = User.GetUserId();
        if (bidderId is null)
        {
            return Unauthorized();
        }

        var requestId =
            HttpContext.Items[IdempotencyFilter.ItemsKey] as string
            ?? throw new InvalidOperationException(
                "IdempotencyKey was not set by IdempotencyFilter."
            );

        // Derived key prevents one user from replaying another user's request.
        var idempotencyKey = $"{bidderId.Value}:{auctionId}:{requestId}";
        var command = new PlaceBidCommand(
            auctionId,
            bidderId.Value,
            request.Amount,
            request.Currency,
            idempotencyKey
        );
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        BidsPlaced.Inc();
        return Created($"/api/v1/auctions/{auctionId}/bids/{result.Value}", result.Value);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<AuctionBidDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBids(
        [FromRoute] Guid auctionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        // Bidding is never public: login is required and other bidders' identities are never
        // returned, only which bids belong to the caller.
        var callerId = User.GetUserId();
        if (callerId is null)
        {
            return Unauthorized();
        }

        if (page < 1)
        {
            page = 1;
        }

        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = new GetAuctionBidsQuery(auctionId, page, pageSize, callerId.Value);
        var result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(result.Value);
    }

}
