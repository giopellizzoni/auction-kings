using AuctionKings.Application.DTOs;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(string Email, string DisplayName, string Password) : IRequest<Result<UserDto>>
{
    // A record prints every property; keep the password out of anything that logs this command.
    public override string ToString() => $"{nameof(RegisterUserCommand)} {{ Email = {Email}, DisplayName = {DisplayName} }}";
}
