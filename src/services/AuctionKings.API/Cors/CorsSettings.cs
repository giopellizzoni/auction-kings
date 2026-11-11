namespace AuctionKings.API.Cors;

/// <summary>
/// Cross-origin settings bound from the <c>Cors</c> configuration section.
/// An empty <see cref="AllowedOrigins"/> list means no cross-origin browser access to the REST API.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Exact browser origins (scheme + host + optional non-default port, no path or trailing slash),
    /// e.g. <c>https://app.example.com</c>. Wildcards are rejected at startup because the policy
    /// allows credentials, which SignalR's browser client requires.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}
