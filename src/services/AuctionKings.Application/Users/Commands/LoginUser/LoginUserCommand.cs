using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Users.Commands.LoginUser;

/// <summary>Checks a user's credentials. The API mints the access token from the returned user.</summary>
public sealed record LoginUserCommand(string Email, string Password) : IRequest<Result<UserDto>>
{
    // A record prints every property; keep the password out of anything that logs this command.
    public override string ToString() => $"{nameof(LoginUserCommand)} {{ Email = {Email} }}";
}
