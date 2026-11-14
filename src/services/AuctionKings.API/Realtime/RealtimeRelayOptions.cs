using Microsoft.Extensions.Options;

namespace AuctionKings.API.Realtime;

/// <summary>Tunables of <see cref="RealtimeStreamRelay"/>, bound from the <c>RealtimeRelay</c> section.</summary>
public sealed class RealtimeRelayOptions
{
    public const string SectionName = "RealtimeRelay";

    /// <summary>How long to wait before polling the stream again when it was empty.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>How long to wait after a Redis error before retrying.</summary>
    public TimeSpan ErrorDelay { get; init; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; init; } = 50;

    /// <summary>How long an entry may sit unacknowledged with a consumer before another replica takes it over.</summary>
    public TimeSpan ClaimIdle { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How often a replica looks for entries abandoned by another one.</summary>
    public TimeSpan ClaimEvery { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Entries older than this are dropped: a push that late is noise, not news.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMinutes(10);
}

internal sealed class RealtimeRelayOptionsValidator : IValidateOptions<RealtimeRelayOptions>
{
    public ValidateOptionsResult Validate(string? name, RealtimeRelayOptions options)
    {
        var failures = new List<string>();

        Require(options.PollInterval > TimeSpan.Zero, nameof(options.PollInterval), failures);
        Require(options.ErrorDelay > TimeSpan.Zero, nameof(options.ErrorDelay), failures);
        Require(options.BatchSize > 0, nameof(options.BatchSize), failures);
        Require(options.ClaimEvery > TimeSpan.Zero, nameof(options.ClaimEvery), failures);
        Require(options.MaxAge > TimeSpan.Zero, nameof(options.MaxAge), failures);

        // Zero is allowed: it makes every pending entry immediately claimable (tests use it).
        if (options.ClaimIdle < TimeSpan.Zero)
        {
            failures.Add($"'{RealtimeRelayOptions.SectionName}:{nameof(options.ClaimIdle)}' must not be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(bool valid, string property, List<string> failures)
    {
        if (!valid)
        {
            failures.Add($"'{RealtimeRelayOptions.SectionName}:{property}' must be greater than zero.");
        }
    }
}
