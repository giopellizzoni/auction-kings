namespace AuctionKings.Application.Abstractions;

public interface IConnectionTracker
{
    Task TrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task UntrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
