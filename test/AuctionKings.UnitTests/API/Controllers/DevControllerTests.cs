using AuctionKings.API.Auth;
using AuctionKings.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class DevControllerTests
{
    private const string Key = "staging-dev-token-key-0123456789abcdef0123456789";

    private static readonly TokenRequest Request = new(Guid.NewGuid(), "Admin");

    private static DevController Controller(bool enabled, string? key)
    {
        var jwt = Options.Create(new JwtOptions
        {
            Secret = new string('s', 64),
            Issuer = "auction-kings-tests",
            Audience = "auction-kings-tests",
        });

        return new DevController(
            new JwtTokenGenerator(jwt),
            Options.Create(new DevTokenOptions { EnableDevTokenEndpoint = enabled, DevTokenKey = key }));
    }

    [Fact]
    public void Disabled_endpoint_returns_404_even_with_the_right_key()
    {
        var result = Controller(enabled: false, key: Key).GenerateToken(Request, Key);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Without_a_configured_key_the_endpoint_answers_without_a_header()
    {
        // Development only: DevTokenOptionsValidator stops startup elsewhere when no key is configured.
        var result = Controller(enabled: true, key: null).GenerateToken(Request, key: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.False(string.IsNullOrEmpty(Assert.IsType<TokenResponse>(ok.Value).Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("staging-dev-token-key-0123456789abcdef012345678")] // one character short
    [InlineData("STAGING-DEV-TOKEN-KEY-0123456789ABCDEF0123456789")] // comparison is case-sensitive
    public void With_a_configured_key_a_missing_or_wrong_header_returns_404(string? header)
    {
        var result = Controller(enabled: true, key: Key).GenerateToken(Request, header);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void With_a_configured_key_the_right_header_gets_a_token()
    {
        var result = Controller(enabled: true, key: Key).GenerateToken(Request, Key);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.False(string.IsNullOrEmpty(Assert.IsType<TokenResponse>(ok.Value).Token));
    }
}
