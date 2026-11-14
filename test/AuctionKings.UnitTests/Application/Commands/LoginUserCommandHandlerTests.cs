using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Users.Commands.LoginUser;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.Errors;
using NSubstitute;

namespace AuctionKings.UnitTests.Application.Commands;

public sealed class LoginUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly LoginUserCommandHandler _handler;

    private static readonly User Ann =
        User.Register("ann@example.com", "Ann", "stored-hash", DateTime.UtcNow).Value;

    public LoginUserCommandHandlerTests()
    {
        _users.GetByEmailAsync("ann@example.com", Arg.Any<CancellationToken>()).Returns(Ann);
        _hasher.Verify("right", "stored-hash").Returns(true);
        _handler = new LoginUserCommandHandler(_users, _hasher);
    }

    [Fact]
    public async Task LoginUserCommandHandler_RightPassword_ReturnsTheUser()
    {
        var result = await _handler.Handle(new LoginUserCommand("ann@example.com", "right"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Ann.Id, result.Value.Id);
        Assert.Equal("Bidder", result.Value.Role);
    }

    [Fact]
    public async Task LoginUserCommandHandler_WrongPassword_ReturnsInvalidCredentials()
    {
        var result = await _handler.Handle(new LoginUserCommand("ann@example.com", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.User.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginUserCommandHandler_UnknownEmail_StillVerifies_AndReturnsTheSameError()
    {
        var result = await _handler.Handle(new LoginUserCommand("nobody@example.com", "right"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.User.InvalidCredentials, result.Error);
        // The dummy verification keeps an unknown email as slow as a wrong password.
        _hasher.Received(1).Verify("right", null);
    }

    [Fact]
    public void LoginUserCommand_ToString_DoesNotContainThePassword()
    {
        Assert.DoesNotContain("hunter2", new LoginUserCommand("ann@example.com", "hunter2").ToString(), StringComparison.Ordinal);
    }
}
