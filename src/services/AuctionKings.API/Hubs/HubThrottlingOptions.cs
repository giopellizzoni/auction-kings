using Microsoft.Extensions.Options;

namespace AuctionKings.API.Hubs;

/// <summary>Abuse limits for the SignalR hubs, bound from the <c>HubThrottling</c> section.</summary>
public sealed class HubThrottlingOptions
{
    public const string SectionName = "HubThrottling";

    /// <summary>Hub method invocations allowed per user within <see cref="Window"/>, across all connections and hubs.</summary>
    public int MaxInvocations { get; init; } = 20;

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Concurrent connections allowed per user. <c>AuctionHub</c> and <c>NotificationHub</c> share
    /// one count, so a browser tab with both hubs open uses two.
    /// </summary>
    public int MaxConnectionsPerUser { get; init; } = 10;

    /// <summary>Auctions a single connection may be joined to at once.</summary>
    public int MaxGroupsPerConnection { get; init; } = 20;

    /// <summary>Largest message a client may send. Hub arguments are a single GUID, so this is generous.</summary>
    public int MaxMessageSizeBytes { get; init; } = 8 * 1024;
}

internal sealed class HubThrottlingOptionsValidator : IValidateOptions<HubThrottlingOptions>
{
    public ValidateOptionsResult Validate(string? name, HubThrottlingOptions options)
    {
        var failures = new List<string>();

        Require(options.MaxInvocations > 0, nameof(options.MaxInvocations), failures);
        Require(options.Window > TimeSpan.Zero, nameof(options.Window), failures);
        Require(options.MaxConnectionsPerUser > 0, nameof(options.MaxConnectionsPerUser), failures);
        Require(options.MaxGroupsPerConnection > 0, nameof(options.MaxGroupsPerConnection), failures);
        Require(options.MaxMessageSizeBytes > 0, nameof(options.MaxMessageSizeBytes), failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(bool valid, string property, List<string> failures)
    {
        if (!valid)
        {
            failures.Add($"'{HubThrottlingOptions.SectionName}:{property}' must be greater than zero.");
        }
    }
}
