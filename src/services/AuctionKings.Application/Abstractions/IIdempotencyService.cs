namespace AuctionKings.Application.Abstractions;

public interface IIdempotencyService
{
    /// <summary>Checks the Redis fast-path cache for a completed response.</summary>
    Task<string?> GetCachedResponseAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts an atomic INSERT. Returns <see cref="IdempotencyReservation.Reserved"/> if this
    /// caller owns the slot, <see cref="IdempotencyReservation.InFlight"/> when another request is
    /// processing the same key, or <see cref="IdempotencyReservation.Completed(string)"/> when the
    /// key was already completed (includes the stored response JSON).
    /// </summary>
    Task<IdempotencyReservation> TryReserveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Persists the serialized response and writes the Redis fast-path entry.</summary>
    Task CompleteAsync(string key, string responseJson, CancellationToken cancellationToken = default);

    /// <summary>Deletes the reservation so retries can re-execute (call on handler failure).</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
