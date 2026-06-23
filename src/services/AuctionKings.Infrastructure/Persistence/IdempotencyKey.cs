namespace AuctionKings.Infrastructure.Persistence;

public sealed class IdempotencyKey
{
    public const string StatusReserved = "Reserved";
    public const string StatusCompleted = "Completed";

    public string Key { get; private set; } = string.Empty;
    public string Status { get; set; } = StatusReserved;
    public string? Response { get; set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey CreateReservation(string key, DateTime createdAt, TimeSpan ttl)
    {
        return new IdempotencyKey
        {
            Key = key,
            Status = StatusReserved,
            Response = null,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(ttl),
        };
    }
}
