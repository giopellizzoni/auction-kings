using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Hubs;

public static class HubThrottlingExtensions
{
    /// <summary>
    /// Binds and validates <see cref="HubThrottlingOptions"/>, registers <see cref="HubThrottlingFilter"/>
    /// on every hub, and applies the message-size and concurrency limits. Call after <c>AddSignalR()</c>.
    /// </summary>
    public static IServiceCollection AddHubThrottling(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<HubThrottlingOptions>, HubThrottlingOptionsValidator>();
        services.AddOptions<HubThrottlingOptions>().BindConfiguration(HubThrottlingOptions.SectionName).ValidateOnStart();

        services
            .AddOptions<HubOptions>()
            .Configure<IOptions<HubThrottlingOptions>>(
                (hubOptions, throttling) =>
                {
                    hubOptions.AddFilter<HubThrottlingFilter>();
                    hubOptions.MaximumReceiveMessageSize = throttling.Value.MaxMessageSizeBytes;

                    // One invocation at a time per connection (the SignalR default, pinned here):
                    // a single connection cannot fan out into parallel hub work, and AuctionHub
                    // relies on this for its unsynchronised per-connection state.
                    hubOptions.MaximumParallelInvocationsPerClient = 1;
                }
            );

        return services;
    }
}
