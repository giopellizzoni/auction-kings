using AuctionKings.Domain.ValueObjects;

namespace AuctionKings.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Money_Negative_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Money(-1m, "USD"));
    }

    [Fact]
    public void Money_Zero_IsValid()
    {
        var money = new Money(0m, "USD");
        Assert.Equal(0m, money.Amount);
    }

    [Fact]
    public void Money_Addition_SameCurrency_ShouldSucceed()
    {
        var a = new Money(100m, "USD");
        var b = new Money(50m, "USD");

        var result = a + b;

        Assert.Equal(150m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Money_Addition_DifferentCurrencies_ShouldThrow()
    {
        var usd = new Money(100m, "USD");
        var eur = new Money(100m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd + eur);
    }

    [Fact]
    public void Money_Subtraction_ResultNegative_ShouldThrow()
    {
        var a = new Money(50m, "USD");
        var b = new Money(100m, "USD");

        Assert.Throws<InvalidOperationException>(() => a - b);
    }

    [Fact]
    public void Money_Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "USD");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Money_Equality_DifferentAmount_ShouldNotBeEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(200m, "USD");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Money_Comparison_GreaterThan_ShouldWork()
    {
        var a = new Money(200m, "USD");
        var b = new Money(100m, "USD");

        Assert.True(a > b);
        Assert.False(b > a);
    }

    [Fact]
    public void Money_CurrencyNormalization_IsCaseInsensitive()
    {
        var a = new Money(100m, "usd");
        var b = new Money(100m, "USD");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Money_EmptyCurrency_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Money(100m, ""));
        Assert.Throws<ArgumentException>(() => new Money(100m, "   "));
    }
}
