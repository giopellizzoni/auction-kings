using System.Reflection;
using NetArchTest.Rules;

namespace AuctionKings.ArchitectureTests;

public class NamingConventionTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(AuctionKings.Application.Auctions.Commands.CloseAuction.CloseAuctionCommandHandler).Assembly;

    [Fact]
    public void CommandHandlers_ShouldFollowNamingConvention()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .ResideInNamespaceMatching(@"AuctionKings\.Application\..*\.Commands\..*")
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .HaveNameEndingWith("CommandHandler")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Command handlers must be named *CommandHandler. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void QueryHandlers_ShouldFollowNamingConvention()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .ResideInNamespaceMatching(@"AuctionKings\.Application\..*\.Queries\..*")
            .And()
            .HaveNameEndingWith("Handler")
            .Should()
            .HaveNameEndingWith("QueryHandler")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Query handlers must be named *QueryHandler. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Validators_ShouldNotBePublic()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Validators must be internal. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
