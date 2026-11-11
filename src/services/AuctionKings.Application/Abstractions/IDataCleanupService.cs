namespace AuctionKings.Application.Abstractions;

public interface IDataCleanupService
{
    Task<int> DeleteExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes reservations that never completed (process crashed between the reservation and
    /// <c>CompleteAsync</c>/<c>ReleaseAsync</c>). Without this, retries with the same key would be
    /// rejected as "in-flight" until the 24h expiry.
    /// </summary>
    Task<int> DeleteStaleIdempotencyReservationsAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

    Task<int> DeleteOldOutboxMessagesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}
