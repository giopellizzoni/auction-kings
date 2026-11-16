using Microsoft.Extensions.Options;

namespace AuctionKings.API.Auth;

/// <summary>Fails startup on a half-configured Cognito section instead of rejecting every token at runtime.</summary>
internal sealed class CognitoOptionsValidator(IHostEnvironment environment) : IValidateOptions<CognitoOptions>
{
    public ValidateOptionsResult Validate(string? name, CognitoOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        const string section = CognitoOptions.SectionName;
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            failures.Add($"'{section}:Region' is required when '{section}:UserPoolId' is set.");
        }

        if (options.ClientIds.Length == 0 || options.ClientIds.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"'{section}:ClientIds' must list at least one app client id when '{section}:UserPoolId' is set.");
        }

        if (!Uri.TryCreate(options.ResolvedMetadataAddress, UriKind.Absolute, out var metadata)
            || (metadata.Scheme != Uri.UriSchemeHttps && metadata.Scheme != Uri.UriSchemeHttp))
        {
            failures.Add($"'{section}:MetadataAddress' must be an absolute http(s) URL.");
        }
        else if (metadata.Scheme == Uri.UriSchemeHttp && !environment.IsDevelopment())
        {
            // Plain HTTP is only for the local emulator: elsewhere the signing keys must come over TLS.
            failures.Add($"'{section}:MetadataAddress' must use https outside Development.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
