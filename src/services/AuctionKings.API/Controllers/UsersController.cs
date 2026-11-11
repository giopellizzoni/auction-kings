using AuctionKings.API.Auth;
using AuctionKings.Application.DTOs;
using AuctionKings.Application.Users.Queries.GetUserBidHistory;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

[Route("api/v1/users")]
[Authorize]
public sealed class UsersController : ApiControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => _sender = sender;

    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(PagedResult<BidDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBidHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var callerId = User.GetUserId();
        if (callerId is null)
        {
            return Unauthorized();
        }

        if (callerId.Value != id && !User.IsInRole(Policies.Admin))
        {
            return Forbid();
        }

        if (page < 1)
        {
            page = 1;
        }

        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = new GetUserBidHistoryQuery(id, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(result.Value);
    }

}
