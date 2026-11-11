using AuctionKings.API.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AuctionKings.API.Controllers;

public sealed record TokenRequest(Guid UserId, string Role = "Bidder");
public sealed record TokenResponse(string Token);

[Route("dev")]
[ApiController]
public sealed class DevController : ControllerBase
{
    private readonly JwtTokenGenerator _generator;
    private readonly IConfiguration _config;

    public DevController(JwtTokenGenerator generator, IConfiguration config)
    {
        _generator = generator;
        _config = config;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        if (!_config.GetValue<bool>("Features:EnableDevTokenEndpoint"))
        {
            return NotFound();
        }

        var token = _generator.Generate(request.UserId, request.Role);
        return Ok(new TokenResponse(token));
    }
}
