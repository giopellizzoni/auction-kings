using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuctionKings.API.Auth;

/// <summary>
/// Two bearer schemes behind one default:
/// <list type="bullet">
/// <item><see cref="CognitoScheme"/> — RS256 access tokens from the Cognito user pool (the real identity provider);</item>
/// <item><see cref="LocalScheme"/> — HS256 tokens signed with <c>Jwt:Secret</c>, minted only by <c>/dev/token</c>
/// and by tests.</item>
/// </list>
/// The default scheme looks at the token's (unvalidated) issuer only to pick which scheme validates it.
/// </summary>
internal static class AuthenticationSetup
{
    internal const string LocalScheme = "LocalJwt";
    internal const string CognitoScheme = "Cognito";

    private static readonly JsonWebTokenHandler TokenReader = new();

    internal static IServiceCollection AddAuctionKingsAuthentication(
        this IServiceCollection services,
        JwtOptions jwt,
        CognitoOptions cognito
    )
    {
        var authentication = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddPolicyScheme(
                JwtBearerDefaults.AuthenticationScheme,
                "Cognito or local JWT",
                options => options.ForwardDefaultSelector = context => SelectScheme(context, cognito)
            )
            .AddJwtBearer(
                LocalScheme,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    };
                    options.Events = new JwtBearerEvents { OnMessageReceived = ReadHubToken };
                }
            );

        if (cognito.Enabled)
        {
            authentication.AddJwtBearer(
                CognitoScheme,
                options =>
                {
                    options.MetadataAddress = cognito.ResolvedMetadataAddress;
                    options.RequireHttpsMetadata = cognito.ResolvedMetadataAddress.StartsWith(
                        "https://",
                        StringComparison.OrdinalIgnoreCase
                    );
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = cognito.Issuer,
                        // Cognito access tokens carry no "aud"; the app client is checked in OnTokenValidated.
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                        RoleClaimType = CognitoOptions.GroupsClaimType,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = ReadHubToken,
                        OnTokenValidated = context =>
                        {
                            var failure = CheckCognitoAccessToken(context.Principal, cognito);
                            if (failure is not null)
                            {
                                context.Fail(failure);
                            }

                            return Task.CompletedTask;
                        },
                    };
                }
            );
        }

        return services;
    }

    /// <summary>
    /// Only access tokens from an allowed app client: an ID token (<c>token_use</c> = <c>id</c>) or a token from
    /// another client of the same pool is rejected. Returns the reason, or null when the token is acceptable.
    /// </summary>
    internal static string? CheckCognitoAccessToken(System.Security.Claims.ClaimsPrincipal? principal, CognitoOptions cognito)
    {
        if (principal?.FindFirst("token_use")?.Value != "access")
        {
            return "Only Cognito access tokens are accepted.";
        }

        var clientId = principal.FindFirst("client_id")?.Value;
        return clientId is not null && cognito.ClientIds.Contains(clientId, StringComparer.Ordinal)
            ? null
            : "The token was issued to an app client this API does not accept.";
    }

    /// <summary>Picks the scheme from the token's issuer; anything not from the Cognito pool goes to the local scheme.</summary>
    internal static string SelectScheme(HttpContext context, CognitoOptions cognito)
    {
        if (!cognito.Enabled)
        {
            return LocalScheme;
        }

        var token = ExtractToken(context.Request);
        if (token is null || !TokenReader.CanReadToken(token))
        {
            return LocalScheme;
        }

        try
        {
            return TokenReader.ReadJsonWebToken(token).Issuer == cognito.Issuer ? CognitoScheme : LocalScheme;
        }
        catch (ArgumentException)
        {
            return LocalScheme; // malformed: let the local scheme reject it
        }
    }

    private static string? ExtractToken(HttpRequest request)
    {
        const string prefix = "Bearer ";
        string header = request.Headers.Authorization.ToString();
        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[prefix.Length..].Trim();
        }

        return HubQueryToken(request);
    }

    /// <summary>
    /// SignalR WebSocket connections cannot send Authorization headers, so the JWT is passed as the
    /// <c>access_token</c> query parameter on <c>/hubs</c> requests.
    /// </summary>
    private static string? HubQueryToken(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/hubs"))
        {
            return null;
        }

        var token = request.Query["access_token"].ToString();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static Task ReadHubToken(MessageReceivedContext context)
    {
        var token = HubQueryToken(context.Request);
        if (token is not null)
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }
}
