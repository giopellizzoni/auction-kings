namespace AuctionKings.Domain.ValueObjects;

public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0)
        {
            throw new InvalidOperationException("Subtraction would result in a negative amount.");
        }

        return new Money(result, Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    public bool IsGreaterThanOrEqualTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount >= other.Amount;
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static bool operator >(Money left, Money right) => left.IsGreaterThan(right);
    public static bool operator <(Money left, Money right) => right.IsGreaterThan(left);
    public static bool operator >=(Money left, Money right) => left.IsGreaterThanOrEqualTo(right);
    public static bool operator <=(Money left, Money right) => right.IsGreaterThanOrEqualTo(left);

    private void EnsureSameCurrency(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot perform operation on different currencies: {Currency} and {other.Currency}.");
        }
    }

    public bool Equals(Money? other)
    {
        if (other is null)
        {
            return false;
        }

        return Amount == other.Amount &&
               Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency.ToUpperInvariant());
    public override string ToString() => $"{Amount:F2} {Currency}";

    public static bool operator ==(Money? left, Money? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);
}
