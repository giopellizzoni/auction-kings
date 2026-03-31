using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Application.Auctions.Queries.GetAuctionBids;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

public sealed record PlaceBidRequest(Guid BidderId, decimal Amount, string Currency);

[Route("api/v1/auctions/{auctionId:guid}/bids")]
[ApiController]
public sealed class BidsController : ControllerBase
{
    private readonly ISender _sender;

    public BidsController(ISender sender) => _sender = sender;

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PlaceBid(
        [FromRoute] Guid auctionId,
        [FromBody] PlaceBidRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PlaceBidCommand(auctionId, request.BidderId, request.Amount, request.Currency);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Created($"/api/v1/auctions/{auctionId}/bids/{result.Value}", result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BidDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBids(
        [FromRoute] Guid auctionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = new GetAuctionBidsQuery(auctionId, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(result.Value);
    }

    private ActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Conflict(new { error.Code, error.Message }),
        ErrorType.Unexpected => StatusCode(StatusCodes.Status500InternalServerError, new { error.Code, error.Message }),
        _ => BadRequest(new { error.Code, error.Message }),
    };
}
