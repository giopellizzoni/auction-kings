using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Behaviors;
using AuctionKings.Application.Exceptions;
using MediatR;
using NSubstitute;

namespace AuctionKings.UnitTests.Application;

public sealed class IdempotencyBehaviorTests
{
    private sealed record IdempotentTestCommand(string IdempotencyKey)
        : IRequest<string>,
            IIdempotentCommand;

    private sealed record RegularTestCommand(string Value) : IRequest<string>;

    private readonly IIdempotencyService _idempotencyService =
        Substitute.For<IIdempotencyService>();

    [Fact]
    public async Task IdempotencyBehavior_DuplicateCommand_ShouldThrowDuplicateCommandException()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-123");

        _idempotencyService.ExistsAsync("key-123", Arg.Any<CancellationToken>()).Returns(true);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("result");

        await Assert.ThrowsAsync<DuplicateCommandException>(
            () => behavior.Handle(command, next, CancellationToken.None)
        );
    }

    [Fact]
    public async Task IdempotencyBehavior_NewCommand_ShouldExecuteAndStoreKey()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-456");

        _idempotencyService.ExistsAsync("key-456", Arg.Any<CancellationToken>()).Returns(false);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("result");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("result", result);
        await _idempotencyService.Received(1).StoreAsync("key-456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdempotencyBehavior_NonIdempotentCommand_ShouldPassThrough()
    {
        var behavior = new IdempotencyBehavior<RegularTestCommand, string>(_idempotencyService);
        var command = new RegularTestCommand("value");

        RequestHandlerDelegate<string> next = _ => Task.FromResult("passthrough");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("passthrough", result);
        await _idempotencyService
            .DidNotReceive()
            .ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _idempotencyService
            .DidNotReceive()
            .StoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
