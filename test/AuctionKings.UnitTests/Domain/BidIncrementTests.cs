using AuctionKings.Domain.ValueObjects;

namespace AuctionKings.UnitTests.Domain;

public sealed class BidIncrementTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(50, 5)]
    [InlineData(99.99, 5)]
    [InlineData(100, 10)]
    [InlineData(250, 10)]
    [InlineData(499.99, 10)]
    [InlineData(500, 25)]
    [InlineData(750, 25)]
    [InlineData(999.99, 25)]
    [InlineData(1000, 50)]
    [InlineData(2500, 50)]
    [InlineData(4999.99, 50)]
    [InlineData(5000, 100)]
    [InlineData(10000, 100)]
    public void BidIncrement_ShouldReturnCorrectMinimumForPriceRange(
        decimal currentPrice, decimal expectedIncrement)
    {
        var price = new Money(currentPrice, "USD");

        var increment = BidIncrement.GetMinimumIncrement(price);

        Assert.Equal(expectedIncrement, increment.Amount);
        Assert.Equal("USD", increment.Currency);
    }

    [Fact]
    public void BidIncrement_GetMinimumBid_ShouldBePricePlusIncrement()
    {
        var price = new Money(100m, "USD"); // increment = 10

        var minimumBid = BidIncrement.GetMinimumBid(price);

        Assert.Equal(110m, minimumBid.Amount);
    }

    [Fact]
    public void BidIncrement_PreserveCurrency()
    {
        var price = new Money(100m, "EUR");

        var increment = BidIncrement.GetMinimumIncrement(price);

        Assert.Equal("EUR", increment.Currency);
    }
}
