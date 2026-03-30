using AuctionKings.API.Controllers;
using AuctionKings.Application.Auctions.Commands.CreateAuction;
using AuctionKings.Application.Auctions.Queries.GetActiveAuctions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class AuctionsControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly AuctionsController _controller;

    public AuctionsControllerTests()
    {
        _controller = new AuctionsController(_sender);
    }

    [Fact]
    public async Task AuctionsController_CreateAuction_ShouldReturn201WithLocation()
    {
        var auctionId = Guid.NewGuid();
        _sender.Send(Arg.Any<CreateAuctionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(auctionId));

        var request = new CreateAuctionRequest("Test Auction", "Description", 100m, "USD", 24, Guid.NewGuid());

        var actionResult = await _controller.CreateAuction(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(nameof(AuctionsController.GetAuctionById), createdResult.ActionName);
        Assert.Equal(auctionId, createdResult.RouteValues!["id"]);
    }

    [Fact]
    public async Task AuctionsController_GetAuctions_ShouldReturnPaginatedList()
    {
        var pagedResult = new PagedResult<AuctionSummaryDto>(
            new List<AuctionSummaryDto>
            {
                new(Guid.NewGuid(), "Auction 1", 100m, "USD", DateTime.UtcNow.AddDays(1), AuctionStatus.Active, 0),
                new(Guid.NewGuid(), "Auction 2", 200m, "USD", DateTime.UtcNow.AddDays(2), AuctionStatus.Active, 0),
            },
            totalCount: 2,
            page: 1,
            pageSize: 20);

        _sender.Send(Arg.Any<GetActiveAuctionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(pagedResult));

        var actionResult = await _controller.GetAuctions(1, 20, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var returned = Assert.IsType<PagedResult<AuctionSummaryDto>>(okResult.Value);
        Assert.Equal(2, returned.TotalCount);
        Assert.Equal(2, returned.Items.Count);
    }

    [Fact]
    public async Task AuctionsController_GetAuctionById_NotFound_ShouldReturn404()
    {
        _sender.Send(Arg.Any<GetAuctionDetailsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuctionDetailDto>(DomainErrors.Auction.NotFound));

        var actionResult = await _controller.GetAuctionById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(actionResult);
    }
}
