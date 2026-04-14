using System.Reflection;
using AuctionKings.Shared.Common;
using NetArchTest.Rules;

namespace AuctionKings.ArchitectureTests;

public class DomainRulesTests
{
    private static readonly Assembly DomainAssembly =
        typeof(AuctionKings.Domain.Events.AuctionClosedEvent).Assembly;

    [Fact]
    public void DomainEvents_ShouldImplementIDomainEvent()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .HaveNameEndingWith("Event")
            .Should()
            .ImplementInterface(typeof(IDomainEvent))
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain events must implement IDomainEvent. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Entities_ShouldInheritFromEntityBase()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .ResideInNamespace("AuctionKings.Domain.Aggregates")
            .Should()
            .Inherit(typeof(Entity))
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"All domain aggregates must inherit from Entity. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
