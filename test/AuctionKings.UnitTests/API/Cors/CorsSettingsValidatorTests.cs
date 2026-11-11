using AuctionKings.API.Cors;

namespace AuctionKings.UnitTests.API.Cors;

public sealed class CorsSettingsValidatorTests
{
    private readonly CorsSettingsValidator _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenNoOriginsConfigured()
    {
        var result = _validator.Validate(null, new CorsSettings());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://APP.example.com")]
    public void Validate_ShouldSucceed_ForExactOrigins(string origin)
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = [origin] });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com?x=1")]
    [InlineData("https://app.example.com:443")]
    [InlineData("https://user@app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("app.example.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_ForInvalidOrigins(string origin)
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = [origin] });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_ShouldReportEveryInvalidOrigin()
    {
        var settings = new CorsSettings { AllowedOrigins = ["https://ok.example.com", "*", "https://bad.example.com/"] };

        var result = _validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures!.Count());
    }
}
