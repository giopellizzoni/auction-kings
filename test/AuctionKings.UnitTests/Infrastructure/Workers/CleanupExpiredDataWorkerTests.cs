using AuctionKings.Application.Abstractions;
using AuctionKings.BackgroundWorkers.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AuctionKings.UnitTests.Infrastructure.Workers;

public sealed class CleanupExpiredDataWorkerTests : IDisposable
{
    private readonly IDataCleanupService _cleanupService = Substitute.For<IDataCleanupService>();
    private readonly CleanupWorkerOptions _options = new() { IntervalHours = 1, OutboxRetentionDays = 7 };
    private readonly CleanupExpiredDataWorker _worker;

    public CleanupExpiredDataWorkerTests()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(IDataCleanupService)).Returns(_cleanupService);
        scope.ServiceProvider.Returns(serviceProvider);
        var asyncScope = new AsyncServiceScope(scope);
        scopeFactory.CreateAsyncScope().Returns(asyncScope);

        _worker = new CleanupExpiredDataWorker(
            scopeFactory,
            Options.Create(_options),
            NullLogger<CleanupExpiredDataWorker>.Instance
        );
    }

    public void Dispose() => _worker.Dispose();

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredIdempotencyKeys()
    {
        _cleanupService.DeleteExpiredIdempotencyKeysAsync(Arg.Any<CancellationToken>()).Returns(5);
        _cleanupService.DeleteOldOutboxMessagesAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(0);

        await _worker.CleanupAsync(CancellationToken.None);

        await _cleanupService.Received(1).DeleteExpiredIdempotencyKeysAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOldOutboxMessages_WithConfiguredRetention()
    {
        _cleanupService.DeleteExpiredIdempotencyKeysAsync(Arg.Any<CancellationToken>()).Returns(0);
        _cleanupService.DeleteOldOutboxMessagesAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(12);

        await _worker.CleanupAsync(CancellationToken.None);

        await _cleanupService.Received(1).DeleteOldOutboxMessagesAsync(
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromDays(_options.OutboxRetentionDays)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteStaleReservations_WithConfiguredAge()
    {
        await _worker.CleanupAsync(CancellationToken.None);

        await _cleanupService.Received(1).DeleteStaleIdempotencyReservationsAsync(
            Arg.Is<TimeSpan>(t => t == TimeSpan.FromMinutes(_options.StaleReservationMinutes)),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenCleanupThrows_ShouldKeepRunning()
    {
        _cleanupService
            .DeleteExpiredIdempotencyKeysAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("transient DB failure"));

        await _worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);

        Assert.NotNull(_worker.ExecuteTask);
        Assert.False(_worker.ExecuteTask.IsFaulted);
        await _cleanupService.Received(1).DeleteExpiredIdempotencyKeysAsync(Arg.Any<CancellationToken>());

        await _worker.StopAsync(CancellationToken.None);
    }
}
