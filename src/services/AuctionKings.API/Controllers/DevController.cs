using System.Security.Cryptography;
using System.Text;
using AuctionKings.API.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Controllers;

public sealed record TokenRequest(Guid UserId, string Role = "Bidder");
public sealed record TokenResponse(string Token);

[Route("dev")]
[ApiController]
public sealed class DevController : ControllerBase
{
    private readonly JwtTokenGenerator _generator;
    private readonly DevTokenOptions _options;

    public DevController(JwtTokenGenerator generator, IOptions<DevTokenOptions> options)
    {
        _generator = generator;
        _options = options.Value;
    }

    /// <summary>
    /// Mints a JWT for any user and role. Disabled unless <c>Features:EnableDevTokenEndpoint</c> is true; when
    /// <c>Features:DevTokenKey</c> is set (required outside Development) the caller must send it in
    /// <c>X-Dev-Token-Key</c>. Every refusal is a 404, so the endpoint's existence is not revealed.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GenerateToken(
        [FromBody] TokenRequest request,
        [FromHeader(Name = DevTokenOptions.KeyHeaderName)] string? key
    )
    {
        if (!_options.EnableDevTokenEndpoint)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(_options.DevTokenKey) && !KeyMatches(_options.DevTokenKey, key))
        {
            return NotFound();
        }

        var token = _generator.Generate(request.UserId, request.Role);
        return Ok(new TokenResponse(token));
    }

    /// <summary>Constant-time comparison; hashing first makes the time independent of the lengths too.</summary>
    private static bool KeyMatches(string expected, string? provided)
    {
        if (provided is null)
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}
