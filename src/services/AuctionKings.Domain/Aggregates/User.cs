using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Aggregates;

/// <summary>
/// A registered account. The aggregate never sees a plain-text password: the application layer hashes it
/// before calling <see cref="Register"/>.
/// </summary>
public sealed class User : AggregateRoot
{
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() : base(Guid.NewGuid()) { }

    /// <summary>Creates a self-registered account. Self-registration always yields a <see cref="UserRole.Bidder"/>.</summary>
    public static Result<User> Register(string email, string displayName, string passwordHash, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<User>(DomainErrors.User.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<User>(DomainErrors.User.DisplayNameRequired);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(DomainErrors.User.PasswordHashRequired);
        }

        return Result.Success(new User
        {
            Email = UserEmail.Normalize(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = UserRole.Bidder,
            CreatedAt = createdAt,
        });
    }
}
