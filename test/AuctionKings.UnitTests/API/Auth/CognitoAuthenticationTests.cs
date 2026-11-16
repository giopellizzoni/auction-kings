using System.Security.Claims;
using AuctionKings.API.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Auth;

public sealed class CognitoAuthenticationTests
{
    private static readonly CognitoOptions Cognito = new()
    {
        Region = "us-east-1",
        UserPoolId = "us-east-1_pool",
        ClientIds = ["client-a"],
    };

    private static string UnsignedToken(string issuer) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = issuer });

    private static DefaultHttpContext Request(string? bearer = null, string path = "/api/v1/auctions", string? queryToken = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (bearer is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {bearer}";
        }

        if (queryToken is not null)
        {
            context.Request.QueryString = new QueryString($"?access_token={queryToken}");
        }

        return context;
    }

    [Fact]
    public void Issuer_and_metadata_address_follow_the_Cognito_URL_scheme()
    {
        Assert.Equal("https://cognito-idp.us-east-1.amazonaws.com/us-east-1_pool", Cognito.Issuer);
        Assert.Equal($"{Cognito.Issuer}/.well-known/openid-configuration", Cognito.ResolvedMetadataAddress);
        Assert.Equal("http://moto/x", new CognitoOptions { MetadataAddress = "http://moto/x" }.ResolvedMetadataAddress);
    }

    [Fact]
    public void SelectScheme_sends_a_token_from_the_pool_to_Cognito()
    {
        var scheme = AuthenticationSetup.SelectScheme(Request(UnsignedToken(Cognito.Issuer)), Cognito);

        Assert.Equal(AuthenticationSetup.CognitoScheme, scheme);
    }

    [Fact]
    public void SelectScheme_reads_the_hub_query_string_token()
    {
        var request = Request(path: "/hubs/auction", queryToken: UnsignedToken(Cognito.Issuer));

        Assert.Equal(AuthenticationSetup.CognitoScheme, AuthenticationSetup.SelectScheme(request, Cognito));
    }

    [Theory]
    [InlineData("auction-kings-dev")]
    [InlineData("https://cognito-idp.us-east-1.amazonaws.com/us-east-1_another-pool")]
    public void SelectScheme_sends_any_other_issuer_to_the_local_scheme(string issuer)
    {
        Assert.Equal(AuthenticationSetup.LocalScheme, AuthenticationSetup.SelectScheme(Request(UnsignedToken(issuer)), Cognito));
    }

    [Fact]
    public void SelectScheme_uses_the_local_scheme_without_a_readable_token_or_when_Cognito_is_off()
    {
        Assert.Equal(AuthenticationSetup.LocalScheme, AuthenticationSetup.SelectScheme(Request(), Cognito));
        Assert.Equal(AuthenticationSetup.LocalScheme, AuthenticationSetup.SelectScheme(Request("not-a-jwt"), Cognito));
        Assert.Equal(
            AuthenticationSetup.LocalScheme,
            AuthenticationSetup.SelectScheme(Request(UnsignedToken(Cognito.Issuer)), new CognitoOptions()));
    }

    [Theory]
    [InlineData("access", "client-a", true)]
    [InlineData("id", "client-a", false)]
    [InlineData("access", "client-b", false)]
    [InlineData(null, "client-a", false)]
    [InlineData("access", null, false)]
    public void CheckCognitoAccessToken_accepts_only_access_tokens_of_an_allowed_client(
        string? tokenUse, string? clientId, bool accepted)
    {
        var claims = new List<Claim>();
        if (tokenUse is not null)
        {
            claims.Add(new Claim("token_use", tokenUse));
        }

        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        var failure = AuthenticationSetup.CheckCognitoAccessToken(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), Cognito);

        Assert.Equal(accepted, failure is null);
    }

    private static CognitoOptionsValidator Validator(bool development)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(development ? Environments.Development : Environments.Staging);
        return new CognitoOptionsValidator(environment);
    }

    [Fact]
    public void Validator_accepts_Cognito_turned_off()
    {
        Assert.True(Validator(development: false).Validate(null, new CognitoOptions()).Succeeded);
    }

    [Fact]
    public void Validator_accepts_a_complete_configuration()
    {
        Assert.True(Validator(development: false).Validate(null, Cognito).Succeeded);
    }

    [Fact]
    public void Validator_requires_region_and_a_client_id_once_a_pool_is_set()
    {
        var result = Validator(development: true).Validate(null, new CognitoOptions { UserPoolId = "us-east-1_pool" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Region", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, f => f.Contains("ClientIds", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_allows_an_http_metadata_address_only_in_Development()
    {
        var emulator = new CognitoOptions
        {
            Region = "us-east-1",
            UserPoolId = "us-east-1_pool",
            ClientIds = ["client-a"],
            MetadataAddress = "http://localhost:5555/us-east-1_pool/.well-known/openid-configuration",
        };

        Assert.True(Validator(development: true).Validate(null, emulator).Succeeded);
        Assert.True(Validator(development: false).Validate(null, emulator).Failed);
    }
}
