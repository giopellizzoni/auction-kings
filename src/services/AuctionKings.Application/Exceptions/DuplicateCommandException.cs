namespace AuctionKings.Application.Exceptions;

/// <summary>
/// Thrown when a command with an idempotency key that has already been processed is received.
/// We throw rather than silently return because commands in this system return Result&lt;T&gt;
/// with domain-specific values (e.g. a new BidId). Returning a cached result would require
/// storing and deserializing that value, adding complexity for minimal benefit.
/// Callers should treat this as a signal that the operation already succeeded.
/// </summary>
public sealed class DuplicateCommandException : Exception
{
    public string IdempotencyKey { get; }

    public DuplicateCommandException(string idempotencyKey)
        : base($"Command with idempotency key '{idempotencyKey}' has already been processed.")
    {
        IdempotencyKey = idempotencyKey;
    }
}
