namespace AuctionKings.Application.Abstractions;

public interface IIdempotencyService
{
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task StoreAsync(string key, CancellationToken cancellationToken = default);
}
