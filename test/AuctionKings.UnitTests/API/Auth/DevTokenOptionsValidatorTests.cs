using AuctionKings.API.Auth;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Auth;

public sealed class DevTokenOptionsValidatorTests
{
    private static readonly string ValidKey = new('k', DevTokenOptionsValidator.MinimumKeyBytes);

    private static DevTokenOptionsValidator ValidatorFor(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new DevTokenOptionsValidator(environment);
    }

    private static DevTokenOptions Options(bool enabled, string? key) =>
        new() { EnableDevTokenEndpoint = enabled, DevTokenKey = key };

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Disabled_endpoint_needs_no_key_anywhere(string environmentName)
    {
        var result = ValidatorFor(environmentName).Validate(null, Options(enabled: false, key: null));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Development_allows_the_endpoint_without_a_key()
    {
        var result = ValidatorFor("Development").Validate(null, Options(enabled: true, key: null));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Staging", null)]
    [InlineData("Staging", "")]
    [InlineData("Production", "   ")]
    public void Outside_Development_an_enabled_endpoint_without_a_key_fails_startup(string environmentName, string? key)
    {
        var result = ValidatorFor(environmentName).Validate(null, Options(enabled: true, key: key));

        Assert.True(result.Failed);
        Assert.Contains("'Features:DevTokenKey' is required", result.FailureMessage);
        Assert.Contains(environmentName, result.FailureMessage);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void A_short_key_fails_in_every_environment(string environmentName)
    {
        var shortKey = new string('k', DevTokenOptionsValidator.MinimumKeyBytes - 1);

        var result = ValidatorFor(environmentName).Validate(null, Options(enabled: true, key: shortKey));

        Assert.True(result.Failed);
        Assert.Contains("at least", result.FailureMessage);
        Assert.DoesNotContain(shortKey, result.FailureMessage);
    }

    [Fact]
    public void Staging_with_a_long_enough_key_starts()
    {
        var result = ValidatorFor("Staging").Validate(null, Options(enabled: true, key: ValidKey));

        Assert.True(result.Succeeded);
    }
}
