using AuctionKings.Application.Auctions.Commands.CloseAuction;
using AuctionKings.Application.Auctions.Commands.CreateAuction;
using AuctionKings.Application.Auctions.Queries.GetActiveAuctions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

public sealed record CreateAuctionRequest(
    string Title,
    string Description,
    decimal StartingPrice,
    string Currency,
    double DurationInHours,
    Guid SellerId);

[Route("api/auctions")]
[ApiController]
public sealed class AuctionsController : ControllerBase
{
    private readonly ISender _sender;

    public AuctionsController(ISender sender) => _sender = sender;

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAuction(
        [FromBody] CreateAuctionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAuctionCommand(
            request.Title,
            request.Description,
            request.StartingPrice,
            request.Currency,
            TimeSpan.FromHours(request.DurationInHours),
            request.SellerId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return CreatedAtAction(nameof(GetAuctionById), new { id = result.Value }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuctionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuctions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Min(pageSize, 50);

        var query = new GetActiveAuctionsQuery(page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuctionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAuctionDetailsQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseAuction(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new CloseAuctionCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return NoContent();
    }

    private ActionResult MapError(Error error) =>
        error.Code.Contains("NotFound")
            ? NotFound(new { error.Code, error.Message })
            : error.Code.Contains("Conflict")
                ? Conflict(new { error.Code, error.Message })
                : BadRequest(new { error.Code, error.Message });
}
