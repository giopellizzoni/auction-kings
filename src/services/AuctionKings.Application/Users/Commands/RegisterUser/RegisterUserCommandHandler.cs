using AuctionKings.Application.Abstractions;
using AuctionKings.Application.DTOs;
using AuctionKings.Application.Exceptions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider
    )
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UserDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<UserDto>(DomainErrors.User.EmailAlreadyRegistered);
        }

        var registered = User.Register(
            request.Email,
            request.DisplayName,
            _passwordHasher.Hash(request.Password),
            _dateTimeProvider.UtcNow
        );

        if (registered.IsFailure)
        {
            return Result.Failure<UserDto>(registered.Error);
        }

        var user = registered.Value;
        await _userRepository.AddAsync(user, cancellationToken);

        try
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Another request registered the same email between the check above and this commit.
            return Result.Failure<UserDto>(DomainErrors.User.EmailAlreadyRegistered);
        }

        return Result.Success(new UserDto(user.Id, user.Email, user.DisplayName, user.Role.ToString(), user.CreatedAt));
    }
}
