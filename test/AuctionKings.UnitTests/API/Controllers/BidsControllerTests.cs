using AuctionKings.API.Controllers;
using AuctionKings.Application.Auctions.Commands.PlaceBid;
using AuctionKings.Application.Auctions.Queries.GetAuctionBids;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class BidsControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly BidsController _controller;

    public BidsControllerTests()
    {
        _controller = new BidsController(_sender);
    }

    [Fact]
    public async Task BidsController_PlaceBid_ShouldReturn201()
    {
        var bidId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();

        _sender.Send(Arg.Any<PlaceBidCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(bidId));

        var request = new PlaceBidRequest(Guid.NewGuid(), 150m, "USD");

        var actionResult = await _controller.PlaceBid(auctionId, request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(actionResult);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(bidId, createdResult.Value);
    }

    [Fact]
    public async Task BidsController_PlaceBid_InvalidData_ShouldReturn400()
    {
        _sender.Send(Arg.Any<PlaceBidCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(DomainErrors.Auction.BidTooLow));

        var request = new PlaceBidRequest(Guid.NewGuid(), 1m, "USD");

        var actionResult = await _controller.PlaceBid(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task BidsController_GetBids_ShouldReturnPaginatedList()
    {
        var auctionId = Guid.NewGuid();
        var bidderId = Guid.NewGuid();
        var pagedResult = new PagedResult<BidDto>(
            new List<BidDto>
            {
                new(Guid.NewGuid(), bidderId, 100m, "USD", DateTime.UtcNow, BidStatus.Active),
            },
            totalCount: 1,
            page: 1,
            pageSize: 20);

        _sender.Send(Arg.Any<GetAuctionBidsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        var actionResult = await _controller.GetBids(auctionId, 1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var returned = Assert.IsType<PagedResult<BidDto>>(okResult.Value);
        Assert.Equal(1, returned.TotalCount);
    }
}
