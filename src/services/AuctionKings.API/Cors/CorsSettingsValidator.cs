using Microsoft.Extensions.Options;

namespace AuctionKings.API.Cors;

/// <summary>
/// Fails startup when <see cref="CorsSettings.AllowedOrigins"/> contains anything other than
/// exact http(s) origins. Browsers send the Origin header as <c>scheme://host[:port]</c>, so
/// entries with a path, trailing slash, query, user info, or wildcard would either never match
/// or (for <c>*</c>) be incompatible with credentialed requests.
/// </summary>
internal sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        var failures = new List<string>();

        foreach (var origin in options.AllowedOrigins)
        {
            if (!IsExactOrigin(origin))
            {
                failures.Add(
                    $"'{CorsSettings.SectionName}:AllowedOrigins' entry '{origin}' is not a valid origin. "
                        + "Use the exact form 'https://host[:port]' with no path, trailing slash, or wildcard."
                );
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsExactOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            return false;
        }

        // GetLeftPart(Authority) yields "scheme://host[:non-default-port]"; anything extra
        // (path, trailing slash, query, explicit default port) makes the strings differ.
        return string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
    }
}
