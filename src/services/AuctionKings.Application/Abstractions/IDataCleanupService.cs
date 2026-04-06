namespace AuctionKings.Application.Abstractions;

public interface IDataCleanupService
{
    Task<int> DeleteExpiredIdempotencyKeysAsync(CancellationToken cancellationToken = default);
    Task<int> DeleteOldOutboxMessagesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}
