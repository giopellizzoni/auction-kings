using System.Text;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Auth;

/// <summary>
/// Fails startup on an unusable JWT signing configuration instead of surfacing as
/// <c>IDX10703</c>/<c>IDX10720</c> on the first request. Secrets are never committed: local
/// development gets a generated one from <c>scripts/setup-local-env.sh</c>, and deployed
/// environments get theirs from a secrets manager.
/// </summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    /// <summary>HS256 requires a key of at least 256 bits.</summary>
    internal const int MinimumSecretBytes = 32;

    /// <summary>
    /// Values that were once committed to the repository (and are therefore public forever).
    /// They are rejected in every environment so they can never sign a token again.
    /// </summary>
    private static readonly string[] KnownPlaceholderSecrets = ["dev-only-secret-key-at-least-32-chars!!"];

    private const string SetupHint =
        "Run ./scripts/setup-local-env.sh to generate one for local development, "
        + "or provide it through the Jwt__Secret environment variable (a secrets manager in deployed environments).";

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        var secret = options.Secret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            failures.Add($"'{JwtOptions.SectionName}:Secret' is not configured. {SetupHint}");
        }
        else if (KnownPlaceholderSecrets.Contains(secret.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                $"'{JwtOptions.SectionName}:Secret' is a placeholder that was committed to source control and is public. "
                    + SetupHint
            );
        }
        else if (Encoding.UTF8.GetByteCount(secret) < MinimumSecretBytes)
        {
            failures.Add(
                $"'{JwtOptions.SectionName}:Secret' must be at least {MinimumSecretBytes} bytes (256 bits) for HS256."
            );
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"'{JwtOptions.SectionName}:Issuer' is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"'{JwtOptions.SectionName}:Audience' is not configured.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
