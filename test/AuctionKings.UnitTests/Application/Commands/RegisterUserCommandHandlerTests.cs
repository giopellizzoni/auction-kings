using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Exceptions;
using AuctionKings.Application.Users.Commands.RegisterUser;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly RegisterUserCommandHandler _handler;

    private static readonly RegisterUserCommand Command = new("Ann@Example.com", "Ann", "a long password");

    public RegisterUserCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _handler = new RegisterUserCommandHandler(_users, _hasher, _unitOfWork, _clock);
    }

    [Fact]
    public async Task RegisterUserCommandHandler_NewEmail_StoresTheHashedPasswordAndReturnsTheUser()
    {
        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ann@example.com", result.Value.Email);
        Assert.Equal("Bidder", result.Value.Role);
        Assert.Equal(Now, result.Value.CreatedAt);
        _hasher.Received(1).Hash("a long password");
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.PasswordHash == "hashed" && u.Id == result.Value.Id),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterUserCommandHandler_TakenEmail_ReturnsConflictAndPersistsNothing()
    {
        _users.EmailExistsAsync(Command.Email, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.User.EmailAlreadyRegistered, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _hasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task RegisterUserCommandHandler_ConcurrentRegistrationOfTheSameEmail_ReturnsConflict()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new UniqueConstraintException("ux_users_email"));

        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.User.EmailAlreadyRegistered, result.Error);
    }

    [Theory]
    [InlineData("not-an-email", "Ann", "a long password", nameof(RegisterUserCommand.Email))]
    [InlineData("ann@example.com", "A", "a long password", nameof(RegisterUserCommand.DisplayName))]
    [InlineData("ann@example.com", "Ann", "short", nameof(RegisterUserCommand.Password))]
    public async Task RegisterUserCommandValidator_RejectsInvalidInput(
        string email, string name, string password, string property)
    {
        var result = await new RegisterUserCommandValidator().ValidateAsync(new RegisterUserCommand(email, name, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void RegisterUserCommand_ToString_DoesNotContainThePassword()
    {
        Assert.DoesNotContain("a long password", Command.ToString(), StringComparison.Ordinal);
    }
}
