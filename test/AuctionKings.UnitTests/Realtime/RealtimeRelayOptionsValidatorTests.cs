using AuctionKings.API.Realtime;

namespace AuctionKings.UnitTests.Realtime;

public class RealtimeRelayOptionsValidatorTests
{
    private readonly RealtimeRelayOptionsValidator _validator = new();

    [Fact]
    public void Validate_Defaults_ShouldSucceed() =>
        Assert.True(_validator.Validate(null, new RealtimeRelayOptions()).Succeeded);

    [Fact]
    public void Validate_ZeroClaimIdle_ShouldSucceed() =>
        Assert.True(_validator.Validate(null, new RealtimeRelayOptions { ClaimIdle = TimeSpan.Zero }).Succeeded);

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Validate_NonPositiveSetting_ShouldFailNamingTheKey(RealtimeRelayOptions options, string key)
    {
        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(key, result.FailureMessage, StringComparison.Ordinal);
    }

    public static TheoryData<RealtimeRelayOptions, string> Invalid() =>
        new()
        {
            { new RealtimeRelayOptions { PollInterval = TimeSpan.Zero }, "RealtimeRelay:PollInterval" },
            { new RealtimeRelayOptions { ErrorDelay = TimeSpan.Zero }, "RealtimeRelay:ErrorDelay" },
            { new RealtimeRelayOptions { BatchSize = 0 }, "RealtimeRelay:BatchSize" },
            { new RealtimeRelayOptions { ClaimEvery = TimeSpan.Zero }, "RealtimeRelay:ClaimEvery" },
            { new RealtimeRelayOptions { MaxAge = TimeSpan.Zero }, "RealtimeRelay:MaxAge" },
            { new RealtimeRelayOptions { ClaimIdle = TimeSpan.FromSeconds(-1) }, "RealtimeRelay:ClaimIdle" },
        };
}
