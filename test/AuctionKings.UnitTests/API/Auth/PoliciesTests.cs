using System.Reflection;
using AuctionKings.API.Auth;
using AuctionKings.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace AuctionKings.UnitTests.API.Auth;

public sealed class PoliciesTests
{
    [Fact]
    public void Policies_BidderConstant_IsCorrectString()
    {
        Assert.Equal("Bidder", Policies.Bidder);
    }

    [Fact]
    public void Policies_AdminConstant_IsCorrectString()
    {
        Assert.Equal("Admin", Policies.Admin);
    }

    [Fact]
    public void BidsController_PlaceBid_HasBidderPolicy()
    {
        var method = typeof(BidsController).GetMethod(nameof(BidsController.PlaceBid));
        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(Policies.Bidder, attr.Policy);
    }

    [Fact]
    public void AuctionsController_CloseAuction_HasAdminPolicy()
    {
        var method = typeof(AuctionsController).GetMethod(nameof(AuctionsController.CloseAuction));
        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(Policies.Admin, attr.Policy);
    }
}
