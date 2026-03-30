using AuctionKings.Application.DTOs;
using AuctionKings.Application.Users.Queries.GetUserBidHistory;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

[Route("api/users")]
[ApiController]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => _sender = sender;

    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(PagedResult<BidDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBidHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Min(pageSize, 50);

        var query = new GetUserBidHistoryQuery(id, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
}
