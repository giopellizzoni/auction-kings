namespace AuctionKings.Infrastructure.HealthChecks;

internal interface IDatabasePingService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
