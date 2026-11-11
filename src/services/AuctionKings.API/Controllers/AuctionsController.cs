using AuctionKings.API.Auth;
using AuctionKings.API.Filters;
using AuctionKings.Application.Auctions.Commands.CloseAuction;
using AuctionKings.Application.Auctions.Commands.CreateAuction;
using AuctionKings.Application.Auctions.Queries.GetActiveAuctions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

public sealed record CreateAuctionRequest(
    string Title,
    string Description,
    decimal StartingPrice,
    string Currency,
    double DurationInHours,
    DateTime? StartTime = null
);

[Route("api/v1/auctions")]
public sealed class AuctionsController : ApiControllerBase
{
    private readonly ISender _sender;

    public AuctionsController(ISender sender) => _sender = sender;

    [HttpPost]
    [Authorize(Policy = Policies.Bidder)]
    [RateLimit(policy: "create-auction", failOpen: false)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAuction(
        [FromBody] CreateAuctionRequest request,
        CancellationToken cancellationToken
    )
    {
        var sellerId = User.GetUserId();
        if (sellerId is null)
        {
            return Unauthorized();
        }

        // TimeSpan.FromHours throws for NaN/Infinity/out-of-range values before the validator
        // ever runs, which surfaced as a 500. Range rules (min/max duration) stay in the validator.
        if (!double.IsFinite(request.DurationInHours)
            || Math.Abs(request.DurationInHours) >= TimeSpan.MaxValue.TotalHours)
        {
            return MapError(DomainErrors.Auction.InvalidDuration);
        }

        var command = new CreateAuctionCommand(
            request.Title,
            request.Description,
            request.StartingPrice,
            request.Currency,
            TimeSpan.FromHours(request.DurationInHours),
            sellerId.Value,
            ToUtc(request.StartTime)
        );

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
        CancellationToken cancellationToken = default
    )
    {
        if (page < 1)
        {
            page = 1;
        }

        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = new GetActiveAuctionsQuery(page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuctionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken
    )
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
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseAuction(
        [FromRoute] Guid id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default
    )
    {
        // Without ?force=true an auction can only be closed once it has reached its end time.
        var command = new CloseAuctionCommand(id, force);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Npgsql only accepts <see cref="DateTimeKind.Utc"/> for <c>timestamptz</c> columns.
    /// JSON timestamps with an offset bind as Local, timestamps without one as Unspecified;
    /// the API contract is UTC, so Unspecified is interpreted as UTC.
    /// </summary>
    private static DateTime? ToUtc(DateTime? value) =>
        value switch
        {
            null => null,
            { Kind: DateTimeKind.Utc } utc => utc,
            { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
        };
}
