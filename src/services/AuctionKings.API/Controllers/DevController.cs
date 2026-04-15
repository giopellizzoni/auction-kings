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
    private readonly IHostEnvironment _env;

    public DevController(JwtTokenGenerator generator, IHostEnvironment env)
    {
        _generator = generator;
        _env = env;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        if (_env.IsProduction())
        {
            return NotFound();
        }

        var token = _generator.Generate(request.UserId, request.Role);
        return Ok(new TokenResponse(token));
    }
}
