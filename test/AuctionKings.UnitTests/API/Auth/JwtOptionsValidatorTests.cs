using AuctionKings.API.Auth;

namespace AuctionKings.UnitTests.API.Auth;

public sealed class JwtOptionsValidatorTests
{
    // The key that was committed to appsettings.Development.json before #104.
    private const string CommittedPlaceholder = "dev-only-secret-key-at-least-32-chars!!";

    private readonly JwtOptionsValidator _validator = new();

    private static JwtOptions Options(
        string secret = "0123456789abcdef0123456789abcdef",
        string issuer = "auction-kings-dev",
        string audience = "auction-kings-dev"
    ) => new()
    {
        Secret = secret,
        Issuer = issuer,
        Audience = audience,
    };

    [Fact]
    public void Validate_ShouldSucceed_ForGeneratedLengthSecret()
    {
        var result = _validator.Validate(null, Options(secret: new string('a', 64)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldSucceed_ForSecretAtExactMinimumLength()
    {
        var result = _validator.Validate(null, Options(secret: new string('a', JwtOptionsValidator.MinimumSecretBytes)));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenSecretIsMissing_AndPointToSetupScript(string secret)
    {
        var result = _validator.Validate(null, Options(secret: secret));

        Assert.True(result.Failed);
        Assert.Contains("setup-local-env.sh", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSecretIsShorterThanHs256Minimum()
    {
        var result = _validator.Validate(null, Options(secret: new string('a', JwtOptionsValidator.MinimumSecretBytes - 1)));

        Assert.True(result.Failed);
        Assert.Contains("256 bits", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldMeasureBytesNotCharacters()
    {
        // 16 two-byte characters = 32 bytes: long enough. 15 of them = 30 bytes: too short.
        Assert.True(_validator.Validate(null, Options(secret: new string('é', 16))).Succeeded);
        Assert.True(_validator.Validate(null, Options(secret: new string('é', 15))).Failed);
    }

    [Theory]
    [InlineData(CommittedPlaceholder)]
    [InlineData("DEV-ONLY-SECRET-KEY-AT-LEAST-32-CHARS!!")]
    [InlineData("  " + CommittedPlaceholder + "  ")]
    public void Validate_ShouldFail_ForTheCommittedPlaceholder_InAnyCasing(string secret)
    {
        var result = _validator.Validate(null, Options(secret: secret));

        Assert.True(result.Failed);
        Assert.Contains("committed to source control", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenIssuerOrAudienceMissing()
    {
        var result = _validator.Validate(null, Options(issuer: "", audience: " "));

        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public void Validate_ShouldReportEveryProblemAtOnce()
    {
        var result = _validator.Validate(null, new JwtOptions());

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
    }

    [Fact]
    public void DevelopmentSettings_ShouldNotContainSigningSecret()
    {
        // Regression for #104: the secret must never be committed again.
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
        var apiAssemblyDir = Path.GetDirectoryName(typeof(JwtOptions).Assembly.Location)!;
        var candidates = new[] { path, Path.Combine(apiAssemblyDir, "appsettings.Development.json") };

        var file = candidates.FirstOrDefault(File.Exists);
        Assert.NotNull(file);

        var json = File.ReadAllText(file);
        Assert.DoesNotContain(CommittedPlaceholder, json, StringComparison.OrdinalIgnoreCase);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("Jwt", out var jwt) && jwt.TryGetProperty("Secret", out _));
    }
}
