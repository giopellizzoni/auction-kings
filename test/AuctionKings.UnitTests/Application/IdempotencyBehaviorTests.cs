using System.Text.Json;
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
    public async Task IdempotencyBehavior_InFlightDuplicate_ShouldThrowDuplicateCommandException()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-123");

        _idempotencyService.GetCachedResponseAsync("key-123", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _idempotencyService.TryReserveAsync("key-123", Arg.Any<CancellationToken>())
            .Returns(IdempotencyReservation.InFlight);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("result");

        await Assert.ThrowsAsync<DuplicateCommandException>(
            () => behavior.Handle(command, next, CancellationToken.None)
        );
    }

    [Fact]
    public async Task IdempotencyBehavior_CompletedDuplicate_ShouldReturnStoredResponse()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-789");
        var stored = JsonSerializer.Serialize("cached-result");

        _idempotencyService.GetCachedResponseAsync("key-789", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _idempotencyService.TryReserveAsync("key-789", Arg.Any<CancellationToken>())
            .Returns(IdempotencyReservation.Completed(stored));

        RequestHandlerDelegate<string> next = _ => Task.FromResult("fresh-result");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("cached-result", result);
    }

    [Fact]
    public async Task IdempotencyBehavior_RedisCachedResponse_ShouldReturnWithoutDbHit()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-fast");
        var cached = JsonSerializer.Serialize("fast-result");

        _idempotencyService.GetCachedResponseAsync("key-fast", Arg.Any<CancellationToken>())
            .Returns(cached);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("fresh-result");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("fast-result", result);
        await _idempotencyService.DidNotReceive().TryReserveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdempotencyBehavior_NewCommand_ShouldExecuteAndComplete()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-456");

        _idempotencyService.GetCachedResponseAsync("key-456", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _idempotencyService.TryReserveAsync("key-456", Arg.Any<CancellationToken>())
            .Returns(IdempotencyReservation.Reserved);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("result");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("result", result);
        await _idempotencyService.Received(1)
            .CompleteAsync("key-456", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdempotencyBehavior_HandlerThrows_ShouldRelease()
    {
        var behavior = new IdempotencyBehavior<IdempotentTestCommand, string>(_idempotencyService);
        var command = new IdempotentTestCommand("key-fail");

        _idempotencyService.GetCachedResponseAsync("key-fail", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _idempotencyService.TryReserveAsync("key-fail", Arg.Any<CancellationToken>())
            .Returns(IdempotencyReservation.Reserved);

        RequestHandlerDelegate<string> next = _ => throw new InvalidOperationException("handler error");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(command, next, CancellationToken.None)
        );

        await _idempotencyService.Received(1)
            .ReleaseAsync("key-fail", Arg.Any<CancellationToken>());
        await _idempotencyService.DidNotReceive()
            .CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
            .GetCachedResponseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _idempotencyService
            .DidNotReceive()
            .TryReserveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
