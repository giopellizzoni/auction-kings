using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Users.Commands.LoginUser;

public sealed class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<UserDto>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Verify runs even when no account matched (null hash), so the response time does not reveal
        // whether the email is registered.
        if (!_passwordHasher.Verify(request.Password, user?.PasswordHash) || user is null)
        {
            return Result.Failure<UserDto>(DomainErrors.User.InvalidCredentials);
        }

        return Result.Success(new UserDto(user.Id, user.Email, user.DisplayName, user.Role.ToString(), user.CreatedAt));
    }
}
