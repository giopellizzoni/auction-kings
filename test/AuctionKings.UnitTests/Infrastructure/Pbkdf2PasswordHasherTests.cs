using AuctionKings.Infrastructure.Services;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    // Low work factor keeps the tests fast; the format and comparison logic are the same.
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_ThenVerify_AcceptsTheSamePassword_AndRejectsAnother()
    {
        var hash = _hasher.Hash("s3cret-password");

        Assert.True(_hasher.Verify("s3cret-password", hash));
        Assert.False(_hasher.Verify("s3cret-Password", hash));
    }

    [Fact]
    public void Hash_UsesAFreshSalt_AndRecordsTheWorkFactor()
    {
        var first = _hasher.Hash("same");
        var second = _hasher.Hash("same");

        Assert.NotEqual(first, second);
        Assert.StartsWith("pbkdf2-sha512.1000.", first, StringComparison.Ordinal);
        Assert.Equal(4, first.Split('.').Length);
    }

    [Fact]
    public void Verify_UsesTheIterationCountStoredInTheHash()
    {
        var hash = new Pbkdf2PasswordHasher(iterations: 2_000).Hash("pw-123456");

        Assert.True(_hasher.Verify("pw-123456", hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("bcrypt.1000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pbkdf2-sha512.0.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pbkdf2-sha512.99999999.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pbkdf2-sha512.1000.not-base64!.AAAA")]
    [InlineData("pbkdf2-sha512.1000.AAAA.AAAA")]
    public void Verify_TreatsAMissingOrMalformedHashAsAMismatch(string? hash)
    {
        Assert.False(_hasher.Verify("anything", hash));
    }
}
