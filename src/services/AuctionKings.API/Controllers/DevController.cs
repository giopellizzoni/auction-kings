using System.Security.Cryptography;
using System.Text;
using AuctionKings.API.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

public sealed record TokenRequest(Guid UserId, string Role = "Bidder");
public sealed record TokenResponse(string Token);

/// <summary>
/// Issues test JWTs until a real identity provider (Cognito) replaces local HS256 auth.
/// <para>
/// Enabled by <c>Features:EnableDevTokenEndpoint</c>. Outside Development the caller must also send
/// <c>X-Dev-Token-Secret</c> matching <c>Features:DevTokenSecret</c> (injected from Secrets Manager in
/// staging) — the endpoint mints tokens for any user and any role, so behind a public load balancer it
/// must not be anonymous. Every rejection is a 404 so the endpoint's existence is not disclosed.
/// </para>
/// </summary>
[Route("dev")]
[ApiController]
public sealed class DevController : ControllerBase
{
    internal const string SecretHeaderName = "X-Dev-Token-Secret";

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal) { Policies.Bidder, Policies.Admin };

    private readonly JwtTokenGenerator _generator;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;

    public DevController(JwtTokenGenerator generator, IConfiguration config, IHostEnvironment environment)
    {
        _generator = generator;
        _config = config;
        _environment = environment;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        if (!_config.GetValue<bool>("Features:EnableDevTokenEndpoint") || !IsCallerAuthorized())
        {
            return NotFound();
        }

        if (request.UserId == Guid.Empty || !AllowedRoles.Contains(request.Role))
        {
            return BadRequest(new
            {
                code = "DevToken.InvalidRequest",
                message = $"UserId must be non-empty and Role one of: {string.Join(", ", AllowedRoles)}.",
            });
        }

        var token = _generator.Generate(request.UserId, request.Role);
        return Ok(new TokenResponse(token));
    }

    private bool IsCallerAuthorized()
    {
        if (_environment.IsDevelopment())
        {
            return true;
        }

        // Fail closed: enabled-but-unconfigured never issues tokens outside Development.
        var expected = _config["Features:DevTokenSecret"];
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var provided = Request.Headers[SecretHeaderName].ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
