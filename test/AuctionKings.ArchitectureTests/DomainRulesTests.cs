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

    [Fact]
    public void AggregateRoots_ShouldReportBusinessRuleViolationsAsResults()
    {
        // One error contract for the aggregate (issue #116): its public behaviour returns Result or
        // Result<T>, so a rule violation reaches the caller as a value, never as an exception.
        var aggregateRoots = DomainAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(AggregateRoot).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(aggregateRoots); // guards against this rule passing because it matched nothing

        var violations = aggregateRoots
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && !ReturnsResult(m))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name} returns {m.ReturnType.Name}")
            .ToList();

        Assert.True(violations.Count == 0,
            $"Aggregate root methods must return Result or Result<T>. Violations: {string.Join(", ", violations)}");
    }

    private static bool ReturnsResult(MethodInfo method) =>
        method.ReturnType == typeof(Result)
        || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Result<>));
}
