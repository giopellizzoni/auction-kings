using AuctionKings.Infrastructure.Realtime;
using AuctionKings.IntegrationTests.Infrastructure;
using AuctionKings.Shared.Common;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>
/// End to end across the process boundary: the worker side publishes to the Redis Stream, the running API's
/// relay picks it up and the seller's SignalR connection receives it. Nobody else does.
/// </summary>
[Collection("AuthApi")]
public sealed class WorkerToApiNotificationTests(AuthApiFactory factory)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private HubConnection Connect(Guid userId) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/notifications"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(factory.CreateToken(userId, "Bidder"));
                }
            )
            .Build();

    [Fact]
    public async Task A_cancellation_published_by_the_worker_reaches_only_the_seller()
    {
        var sellerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var seller = Connect(sellerId);
        await using var other = Connect(otherId);
        seller.On<object>("AuctionCancelled", payload => sellerGot.TrySetResult(payload.ToString()!));
        other.On<object>("AuctionCancelled", payload => otherGot.TrySetResult(payload.ToString()!));
        await seller.StartAsync();
        await other.StartAsync();

        // Exactly what the worker process does: a stream-backed notification service on the shared Redis.
        using var redis = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var workerSide = new RedisStreamRealtimeNotificationService(
            redis,
            new SystemClock(),
            NullLogger<RedisStreamRealtimeNotificationService>.Instance);
        await workerSide.NotifyAuctionCancelledAsync(sellerId, auctionId, "Vintage watch", DateTime.UtcNow);

        var received = await sellerGot.Task.WaitAsync(Patience);
        Assert.Contains(auctionId.ToString(), received, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vintage watch", received, StringComparison.Ordinal);

        // Give a wrongly delivered copy time to show up before declaring that nobody else got one.
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.False(otherGot.Task.IsCompleted, "A user who is not the seller must not receive the notification.");
    }

    private sealed class SystemClock : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
