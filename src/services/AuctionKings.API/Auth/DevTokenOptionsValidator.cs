using System.Text;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Auth;

/// <summary>
/// Refuses to start when <c>/dev/token</c> is enabled outside Development without a key, so the endpoint can
/// never again be open to anyone on a deployed environment (it mints tokens for any role, including Admin).
/// </summary>
internal sealed class DevTokenOptionsValidator(IHostEnvironment environment) : IValidateOptions<DevTokenOptions>
{
    /// <summary>Long enough that guessing the key is not a practical attack.</summary>
    internal const int MinimumKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, DevTokenOptions options)
    {
        if (!options.EnableDevTokenEndpoint)
        {
            return ValidateOptionsResult.Success;
        }

        var key = options.DevTokenKey;
        const string keySetting = $"'{DevTokenOptions.SectionName}:{nameof(DevTokenOptions.DevTokenKey)}'";

        if (string.IsNullOrWhiteSpace(key))
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    $"{keySetting} is required when '{DevTokenOptions.SectionName}:{nameof(DevTokenOptions.EnableDevTokenEndpoint)}' "
                        + $"is true outside Development (environment '{environment.EnvironmentName}'). Set it from a secrets "
                        + "manager, or disable the endpoint."
                );
        }

        return Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes
            ? ValidateOptionsResult.Fail($"{keySetting} must be at least {MinimumKeyBytes} bytes.")
            : ValidateOptionsResult.Success;
    }
}
