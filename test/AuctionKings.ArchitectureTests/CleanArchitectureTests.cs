using System.Reflection;
using NetArchTest.Rules;

namespace AuctionKings.ArchitectureTests;

public class CleanArchitectureTests
{
    private static readonly Assembly DomainAssembly =
        typeof(AuctionKings.Domain.Events.AuctionClosedEvent).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(AuctionKings.Application.Auctions.Commands.CloseAuction.CloseAuctionCommandHandler).Assembly;
    private static readonly Assembly SharedAssembly =
        typeof(AuctionKings.Shared.Common.Entity).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("AuctionKings.Application")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer must not reference Application. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("AuctionKings.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer must not reference Infrastructure. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("AuctionKings.API")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer must not reference API. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("AuctionKings.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer must not reference Infrastructure. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNotDependOn_API()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("AuctionKings.API")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer must not reference API. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Shared_ShouldNotDependOn_AnyProjectLayer()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AuctionKings.Domain",
                "AuctionKings.Application",
                "AuctionKings.Infrastructure",
                "AuctionKings.API")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Shared layer must not depend on any project layer. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
