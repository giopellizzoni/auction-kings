using AuctionKings.API.Auth;
using AuctionKings.API.Filters;
using AuctionKings.Application.DTOs;
using AuctionKings.Application.Users.Commands.LoginUser;
using AuctionKings.Application.Users.Commands.RegisterUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Controllers;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Bearer token for the <c>Authorization</c> header; <c>ExpiresIn</c> is in seconds.</summary>
public sealed record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn, UserDto User);

/// <summary>
/// Account registration and sign-in with local HS256 JWTs (the same tokens <c>/dev/token</c> mints). Both endpoints
/// are anonymous and rate limited per client IP.
/// </summary>
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController : ApiControllerBase
{
    private readonly ISender _sender;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;

    public AuthController(ISender sender, JwtTokenGenerator tokenGenerator, IOptions<JwtOptions> jwtOptions)
    {
        _sender = sender;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>Creates a <c>Bidder</c> account and signs it in.</summary>
    [HttpPost("register")]
    [RateLimit(policy: "auth-register", failOpen: false)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RegisterUserCommand(request.Email, request.DisplayName, request.Password),
            cancellationToken
        );

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, IssueToken(result.Value));
    }

    /// <summary>Exchanges an email and password for an access token.</summary>
    [HttpPost("login")]
    [RateLimit(policy: "auth-login", failOpen: false)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new LoginUserCommand(request.Email, request.Password), cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error);
        }

        return Ok(IssueToken(result.Value));
    }

    private AuthTokenResponse IssueToken(UserDto user) =>
        new(
            _tokenGenerator.Generate(user.Id, user.Role),
            "Bearer",
            _jwtOptions.ExpiresInMinutes * 60,
            user
        );
}
