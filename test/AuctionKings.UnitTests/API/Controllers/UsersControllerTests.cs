using AuctionKings.API.Controllers;
using AuctionKings.Application.DTOs;
using AuctionKings.Application.Users.Queries.GetUserBidHistory;
using AuctionKings.Domain.Enums;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class UsersControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_sender);
    }

    [Fact]
    public async Task UsersController_GetBidHistory_ShouldReturnPaginatedList()
    {
        var userId = Guid.NewGuid();
        var pagedResult = new PagedResult<BidDto>(
            new List<BidDto>
            {
                new(Guid.NewGuid(), userId, 100m, "USD", DateTime.UtcNow, BidStatus.Won),
                new(Guid.NewGuid(), userId, 200m, "USD", DateTime.UtcNow.AddMinutes(-5), BidStatus.Outbid),
            },
            totalCount: 2,
            page: 1,
            pageSize: 20);

        _sender.Send(Arg.Any<GetUserBidHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        var actionResult = await _controller.GetBidHistory(userId, 1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var returned = Assert.IsType<PagedResult<BidDto>>(okResult.Value);
        Assert.Equal(2, returned.TotalCount);
        Assert.Equal(2, returned.Items.Count);
    }
}
