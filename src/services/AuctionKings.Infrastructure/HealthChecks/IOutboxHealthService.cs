namespace AuctionKings.Infrastructure.HealthChecks;

internal interface IOutboxHealthService
{
    Task<int> CountStuckMessagesAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
