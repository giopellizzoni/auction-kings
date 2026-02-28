namespace AuctionKings.Domain.ValueObjects;

public sealed class AuctionDuration : IEquatable<AuctionDuration>
{
    public static readonly TimeSpan Minimum = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Maximum = TimeSpan.FromDays(30);

    public TimeSpan Value { get; }

    public AuctionDuration(TimeSpan value)
    {
        if (value < Minimum)
            throw new ArgumentException(
                $"Auction duration must be at least {Minimum.TotalMinutes} minutes.", nameof(value));

        if (value > Maximum)
            throw new ArgumentException(
                $"Auction duration cannot exceed {Maximum.TotalDays} days.", nameof(value));

        Value = value;
    }

    public static AuctionDuration FromHours(double hours) => new(TimeSpan.FromHours(hours));
    public static AuctionDuration FromDays(double days) => new(TimeSpan.FromDays(days));

    public DateTime GetEndTime(DateTime startTime) => startTime + Value;

    public bool Equals(AuctionDuration? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is AuctionDuration other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}
