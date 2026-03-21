namespace AuctionKings.Infrastructure.Persistence;

public sealed class IdempotencyKey
{
    public string Key { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(string key, DateTime createdAt, TimeSpan ttl)
    {
        return new IdempotencyKey
        {
            Key = key,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(ttl),
        };
    }
}
