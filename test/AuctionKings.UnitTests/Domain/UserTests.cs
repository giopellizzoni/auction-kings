using AuctionKings.API.Auth;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;

namespace AuctionKings.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_NormalizesTheEmail_TrimsTheName_AndMakesABidder()
    {
        var result = User.Register("  Ann@Example.COM ", "  Ann  ", "hash", Now);

        Assert.True(result.IsSuccess);
        var user = result.Value;
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("ann@example.com", user.Email);
        Assert.Equal("Ann", user.DisplayName);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserRole.Bidder, user.Role);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Theory]
    [InlineData("", "Ann", "hash", "User.EmailRequired")]
    [InlineData("ann@example.com", " ", "hash", "User.DisplayNameRequired")]
    [InlineData("ann@example.com", "Ann", "", "User.PasswordHashRequired")]
    public void Register_RejectsMissingFields(string email, string name, string hash, string code)
    {
        var result = User.Register(email, name, hash, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public void InvalidCredentials_IsAnUnauthorizedError()
    {
        Assert.Equal(AuctionKings.Shared.Common.ErrorType.Unauthorized, DomainErrors.User.InvalidCredentials.Type);
    }

    [Fact]
    public void EveryRole_HasAMatchingAuthorizationPolicy()
    {
        // The role name goes straight into the JWT role claim, which the policies match on.
        Assert.Equal(Policies.Bidder, nameof(UserRole.Bidder));
        Assert.Equal(Policies.Admin, nameof(UserRole.Admin));
        Assert.Equal(2, Enum.GetValues<UserRole>().Length);
    }
}
