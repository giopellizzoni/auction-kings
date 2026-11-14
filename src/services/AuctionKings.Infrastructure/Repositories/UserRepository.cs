using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuctionKings.Infrastructure.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly AuctionDbContext _context;

    public UserRepository(AuctionDbContext context) => _context = context;

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = UserEmail.Normalize(email);
        return _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = UserEmail.Normalize(email);
        return _context.Users.AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }
}
