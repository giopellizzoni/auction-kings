using AuctionKings.Domain.Aggregates;

namespace AuctionKings.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Finds a user by email; the argument is normalized with <see cref="Domain.ValueObjects.UserEmail.Normalize"/>.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
