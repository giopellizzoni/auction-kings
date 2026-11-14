namespace AuctionKings.API.Auth;

/// <summary>
/// Amazon Cognito user pool whose access tokens the API accepts. Cognito is the identity provider: sign-up,
/// sign-in, passwords and MFA all live there, and the API only validates the RS256 tokens it issues. Leaving
/// <see cref="UserPoolId"/> empty turns Cognito off (only the local HS256 tokens of <c>/dev/token</c> work).
/// </summary>
public sealed class CognitoOptions
{
    public const string SectionName = "Cognito";

    /// <summary>Claim Cognito puts group membership in; mapped to roles, so groups <c>Bidder</c>/<c>Admin</c> are roles.</summary>
    public const string GroupsClaimType = "cognito:groups";

    public string Region { get; init; } = string.Empty;

    public string UserPoolId { get; init; } = string.Empty;

    /// <summary>App clients allowed to call the API (checked against the access token's <c>client_id</c>).</summary>
    public string[] ClientIds { get; init; } = [];

    /// <summary>
    /// OpenID configuration URL. Empty means the pool's real one under <see cref="Issuer"/>; set it to reach an
    /// emulator (Moto) whose tokens still carry the real issuer.
    /// </summary>
    public string? MetadataAddress { get; init; }

    public bool Enabled => !string.IsNullOrWhiteSpace(UserPoolId);

    public string Issuer => $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

    public string ResolvedMetadataAddress =>
        string.IsNullOrWhiteSpace(MetadataAddress) ? $"{Issuer}/.well-known/openid-configuration" : MetadataAddress;
}
