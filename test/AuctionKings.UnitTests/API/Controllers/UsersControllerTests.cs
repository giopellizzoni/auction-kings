using System.Security.Claims;
using AuctionKings.API.Controllers;
using AuctionKings.Application.DTOs;
using AuctionKings.Application.Users.Queries.GetUserBidHistory;
using AuctionKings.Domain.Enums;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class UsersControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _callerId = Guid.NewGuid();

    private UsersController BuildController(Guid userId, string role = "Bidder")
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "Test"));

        return new UsersController(_sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claims },
            },
        };
    }

    [Fact]
    public async Task UsersController_GetBidHistory_ShouldReturnPaginatedList()
    {
        var pagedResult = new PagedResult<BidDto>(
            new List<BidDto>
            {
                new(Guid.NewGuid(), _callerId, 100m, "USD", DateTime.UtcNow, BidStatus.Won),
                new(Guid.NewGuid(), _callerId, 200m, "USD", DateTime.UtcNow.AddMinutes(-5), BidStatus.Outbid),
            },
            totalCount: 2,
            page: 1,
            pageSize: 20);

        _sender.Send(Arg.Any<GetUserBidHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        var controller = BuildController(_callerId);
        var actionResult = await controller.GetBidHistory(_callerId, 1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var returned = Assert.IsType<PagedResult<BidDto>>(okResult.Value);
        Assert.Equal(2, returned.TotalCount);
        Assert.Equal(2, returned.Items.Count);
    }

    [Fact]
    public async Task UsersController_GetBidHistory_OtherUser_ShouldReturn403()
    {
        var controller = BuildController(_callerId);
        var otherUserId = Guid.NewGuid();

        var result = await controller.GetBidHistory(otherUserId, 1, 20, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        await _sender.DidNotReceive().Send(Arg.Any<GetUserBidHistoryQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UsersController_GetBidHistory_AdminCanAccessAnyUser()
    {
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var pagedResult = new PagedResult<BidDto>([], totalCount: 0, page: 1, pageSize: 20);
        _sender.Send(Arg.Any<GetUserBidHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        var controller = BuildController(adminId, role: "Admin");
        var result = await controller.GetBidHistory(targetUserId, 1, 20, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UsersController_GetBidHistory_NoClaims_ShouldReturn401()
    {
        var controller = new UsersController(_sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() },
            },
        };

        var result = await controller.GetBidHistory(Guid.NewGuid(), 1, 20, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }
}
