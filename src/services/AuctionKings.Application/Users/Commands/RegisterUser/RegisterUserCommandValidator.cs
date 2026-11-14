using FluentValidation;

namespace AuctionKings.Application.Users.Commands.RegisterUser;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        // Length is the rule that matters (NIST SP 800-63B); the upper bound caps the hashing cost of one request.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength);
    }
}
