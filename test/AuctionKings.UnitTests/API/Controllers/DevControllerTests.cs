using AuctionKings.API.Auth;
using AuctionKings.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Controllers;

public sealed class DevControllerTests
{
    private const string Secret = "s3cret-value-for-tests";

    private static DevController Build(
        string environment,
        bool enabled = true,
        string? configuredSecret = Secret,
        string? headerSecret = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Features:EnableDevTokenEndpoint"] = enabled ? "true" : "false",
            ["Features:DevTokenSecret"] = configuredSecret,
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);

        var generator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Secret = "unit-test-signing-key-at-least-32-characters!",
            Issuer = "test",
            Audience = "test",
        }));

        var httpContext = new DefaultHttpContext();
        if (headerSecret is not null)
        {
            httpContext.Request.Headers[DevController.SecretHeaderName] = headerSecret;
        }

        return new DevController(generator, config, env)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static TokenRequest Request(string role = "Bidder") => new(Guid.NewGuid(), role);

    [Fact]
    public void GenerateToken_Development_WithoutSecret_ShouldIssueToken()
    {
        var result = Build(Environments.Development, configuredSecret: null).GenerateToken(Request());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GenerateToken_Disabled_ShouldReturnNotFound()
    {
        var result = Build(Environments.Development, enabled: false).GenerateToken(Request());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GenerateToken_Staging_WithoutHeader_ShouldReturnNotFound()
    {
        var result = Build(Environments.Staging).GenerateToken(Request("Admin"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GenerateToken_Staging_WithWrongSecret_ShouldReturnNotFound()
    {
        var result = Build(Environments.Staging, headerSecret: "wrong").GenerateToken(Request());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GenerateToken_Staging_WithCorrectSecret_ShouldIssueToken()
    {
        var result = Build(Environments.Staging, headerSecret: Secret).GenerateToken(Request("Admin"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GenerateToken_Staging_WhenSecretNotConfigured_ShouldFailClosed()
    {
        var result = Build(Environments.Staging, configuredSecret: null, headerSecret: "").GenerateToken(Request());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GenerateToken_UnknownRole_ShouldReturnBadRequest()
    {
        var result = Build(Environments.Development).GenerateToken(Request("SuperUser"));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
