namespace AuctionKings.API.Auth;

/// <summary>
/// Settings for <c>POST /dev/token</c>, which mints a JWT for any user ID and role. It exists for local
/// development and demos until real identity (Cognito) lands, so outside Development it must be protected
/// by <see cref="DevTokenKey"/> (see <see cref="DevTokenOptionsValidator"/>).
/// </summary>
public sealed class DevTokenOptions
{
    public const string SectionName = "Features";

    /// <summary>Request header that must carry <see cref="DevTokenKey"/> when a key is configured.</summary>
    public const string KeyHeaderName = "X-Dev-Token-Key";

    /// <summary>When false the endpoint answers 404 as if it did not exist.</summary>
    public bool EnableDevTokenEndpoint { get; init; }

    /// <summary>
    /// Shared secret callers must send in <see cref="KeyHeaderName"/>. Optional in Development; required
    /// everywhere else the endpoint is enabled (staging reads it from Secrets Manager).
    /// </summary>
    public string? DevTokenKey { get; init; }
}
