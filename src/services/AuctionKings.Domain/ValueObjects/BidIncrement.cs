namespace AuctionKings.Domain.ValueObjects;

/// <summary>
/// Defines minimum bid increment rules per price range.
/// Ranges are inclusive lower bound, exclusive upper bound.
/// </summary>
public static class BidIncrement
{
    private static readonly (decimal MaxPrice, decimal Increment)[] Ranges =
    [
        (100m,    5m),
        (500m,    10m),
        (1_000m,  25m),
        (5_000m,  50m),
        (decimal.MaxValue, 100m),
    ];

    public static Money GetMinimumIncrement(Money currentPrice)
    {
        foreach (var (maxPrice, increment) in Ranges)
        {
            if (currentPrice.Amount < maxPrice)
            {
                return new Money(increment, currentPrice.Currency);
            }
        }

        return new Money(Ranges[^1].Increment, currentPrice.Currency);
    }

    public static Money GetMinimumBid(Money currentPrice)
    {
        return currentPrice + GetMinimumIncrement(currentPrice);
    }
}
