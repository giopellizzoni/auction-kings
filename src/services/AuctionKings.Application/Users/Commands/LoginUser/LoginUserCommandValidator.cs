using AuctionKings.Application.Users.Commands.RegisterUser;
using FluentValidation;

namespace AuctionKings.Application.Users.Commands.LoginUser;

internal sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256);

        // No minimum length here: a wrong password must get the same 401 as an unknown account, not a 400.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(RegisterUserCommandValidator.MaxPasswordLength);
    }
}
