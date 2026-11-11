using AuctionKings.API.Hubs;

namespace AuctionKings.UnitTests.API.Hubs;

public sealed class HubThrottlingOptionsTests
{
    private readonly HubThrottlingOptionsValidator _validator = new();

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        Assert.True(_validator.Validate(null, new HubThrottlingOptions()).Succeeded);
    }

    [Fact]
    public void Defaults_ShouldMatchDocumentedLimits()
    {
        var options = new HubThrottlingOptions();

        Assert.Equal(20, options.MaxInvocations);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Window);
        Assert.Equal(10, options.MaxConnectionsPerUser);
        Assert.Equal(20, options.MaxGroupsPerConnection);
        Assert.Equal(8192, options.MaxMessageSizeBytes);
    }

    [Theory]
    [InlineData(nameof(HubThrottlingOptions.MaxInvocations))]
    [InlineData(nameof(HubThrottlingOptions.Window))]
    [InlineData(nameof(HubThrottlingOptions.MaxConnectionsPerUser))]
    [InlineData(nameof(HubThrottlingOptions.MaxGroupsPerConnection))]
    [InlineData(nameof(HubThrottlingOptions.MaxMessageSizeBytes))]
    public void Validate_ShouldFail_WhenASettingIsZero(string property)
    {
        var options = property switch
        {
            nameof(HubThrottlingOptions.MaxInvocations) => new HubThrottlingOptions { MaxInvocations = 0 },
            nameof(HubThrottlingOptions.Window) => new HubThrottlingOptions { Window = TimeSpan.Zero },
            nameof(HubThrottlingOptions.MaxConnectionsPerUser) => new HubThrottlingOptions { MaxConnectionsPerUser = 0 },
            nameof(HubThrottlingOptions.MaxGroupsPerConnection) => new HubThrottlingOptions { MaxGroupsPerConnection = 0 },
            _ => new HubThrottlingOptions { MaxMessageSizeBytes = 0 },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(property, result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNegative()
    {
        Assert.True(_validator.Validate(null, new HubThrottlingOptions { MaxInvocations = -1 }).Failed);
    }

    [Fact]
    public void Validate_ShouldReportEverySettingThatIsWrong()
    {
        var result = _validator.Validate(
            null,
            new HubThrottlingOptions { MaxInvocations = 0, MaxGroupsPerConnection = 0 });

        Assert.Equal(2, result.Failures!.Count());
    }
}
