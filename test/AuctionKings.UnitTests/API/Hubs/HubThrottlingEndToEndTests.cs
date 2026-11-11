using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AuctionKings.API.Hubs;
using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Auctions.Queries.GetAuctionDetails;
using AuctionKings.Application.DTOs;
using AuctionKings.Domain.Enums;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Hubs;

/// <summary>
/// Runs the real hubs, the real <see cref="HubThrottlingFilter"/> and the real SignalR options
/// wiring (<c>AddHubThrottling</c>) in an in-process Kestrel host and drives them with the real
/// SignalR client. Redis-backed services are replaced by in-memory stand-ins, so no Docker is needed.
/// Unit tests of the filter alone cannot prove the registration reaches both hubs.
/// </summary>
public sealed class HubThrottlingEndToEndTests : IAsyncLifetime
{
    private const int MaxInvocations = 3;
    private const int MaxConnectionsPerUser = 2;
    private const int MaxGroupsPerConnection = 2;
    private const int MaxMessageSizeBytes = 1024;

    private static readonly Guid MissingAuctionId = Guid.NewGuid();

    private readonly InMemoryConnectionTracker _tracker = new();
    private readonly List<HubConnection> _connections = [];
    private WebApplication _app = null!;
    private string _baseUrl = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HubThrottling:MaxInvocations"] = MaxInvocations.ToString(CultureInfo.InvariantCulture),
            ["HubThrottling:Window"] = "00:01:00",
            ["HubThrottling:MaxConnectionsPerUser"] = MaxConnectionsPerUser.ToString(CultureInfo.InvariantCulture),
            ["HubThrottling:MaxGroupsPerConnection"] = MaxGroupsPerConnection.ToString(CultureInfo.InvariantCulture),
            ["HubThrottling:MaxMessageSizeBytes"] = MaxMessageSizeBytes.ToString(CultureInfo.InvariantCulture),
        });

        builder.Services.AddSignalR();
        builder.Services.AddHubThrottling();
        builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
        builder.Services.AddSingleton<IConnectionTracker>(_tracker);
        builder.Services.AddSingleton(BuildMediator());
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapHub<AuctionHub>("/hubs/auction");
        _app.MapHub<NotificationHub>("/hubs/notifications");
        await _app.StartAsync();

        _baseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await _app.DisposeAsync();
    }

    [Fact]
    public async Task HubMethod_OverTheLimit_ReachesTheClientAsAHubException()
    {
        var connection = await ConnectAsync(Guid.NewGuid());

        for (var i = 0; i < MaxInvocations; i++)
        {
            await connection.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid());
        }

        var ex = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid()));

        Assert.Contains("Rate limit exceeded", ex.Message);
        Assert.Equal(HubConnectionState.Connected, connection.State); // throttled, not disconnected
    }

    [Fact]
    public async Task OneUsersBudget_IsSharedAcrossConnections_ButNotAcrossUsers()
    {
        var user = Guid.NewGuid();
        var first = await ConnectAsync(user);
        var second = await ConnectAsync(user);
        var otherUser = await ConnectAsync(Guid.NewGuid());

        await first.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid());
        await first.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid());
        await second.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid()); // 3rd call for this user

        await Assert.ThrowsAsync<HubException>(() => second.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid()));
        await Assert.ThrowsAsync<HubException>(() => first.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid()));

        await otherUser.InvokeAsync("RequestStateSyncAsync", Guid.NewGuid()); // unaffected
    }

    [Fact]
    public async Task ConnectionCap_IsSharedAcrossBothHubs_AndAppliesToNotificationHub()
    {
        // NotificationHub has no client-callable methods, so the filter's connection cap is the
        // only throttling it can show: one AuctionHub + one NotificationHub connection fill the
        // cap of 2, so a further NotificationHub connection must be closed.
        var user = Guid.NewGuid();
        var auction = await ConnectAsync(user);
        var notifications = await ConnectAsync(user, "/hubs/notifications");

        var extra = NewConnection(user, "/hubs/notifications");
        await TryStartAsync(extra);

        await EventuallyAsync(() => extra.State == HubConnectionState.Disconnected);
        Assert.Equal(HubConnectionState.Connected, auction.State);
        Assert.Equal(HubConnectionState.Connected, notifications.State);
    }

    [Fact]
    public async Task ConnectionOverTheCap_IsClosedByTheServer_AndFreesItsSlot()
    {
        var user = Guid.NewGuid();
        var first = await ConnectAsync(user);
        var second = await ConnectAsync(user);

        var third = NewConnection(user);
        await TryStartAsync(third);
        await EventuallyAsync(() => third.State == HubConnectionState.Disconnected);

        Assert.Equal(HubConnectionState.Connected, first.State);
        Assert.Equal(HubConnectionState.Connected, second.State);

        // The aborted connection is untracked, so the user is back at the cap — not over it.
        await EventuallyAsync(() => _tracker.Count(user) == MaxConnectionsPerUser);
        await first.StopAsync();
        await EventuallyAsync(() => _tracker.Count(user) == MaxConnectionsPerUser - 1);

        var replacement = await ConnectAsync(user);
        await Task.Delay(300);
        Assert.Equal(HubConnectionState.Connected, replacement.State);
    }

    [Fact]
    public async Task JoiningTooManyAuctions_ReachesTheClientAsAHubException()
    {
        var connection = await ConnectAsync(Guid.NewGuid());

        for (var i = 0; i < MaxGroupsPerConnection; i++)
        {
            await connection.InvokeAsync("JoinAuctionAsync", Guid.NewGuid());
        }

        var ex = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("JoinAuctionAsync", Guid.NewGuid()));

        Assert.Contains($"at most {MaxGroupsPerConnection}", ex.Message);
    }

    [Fact]
    public async Task JoiningAnUnknownAuction_FailsAndDoesNotUseUpAGroupSlot()
    {
        var connection = await ConnectAsync(Guid.NewGuid());

        // Fewer failures than the invocation limit, but more than the group cap would allow.
        for (var i = 0; i < 2; i++)
        {
            var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinAuctionAsync", MissingAuctionId));
            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        var other = await ConnectAsync(Guid.NewGuid());
        await other.InvokeAsync("JoinAuctionAsync", Guid.NewGuid());
        await other.InvokeAsync("JoinAuctionAsync", Guid.NewGuid()); // both slots still available
    }

    [Fact]
    public async Task OversizedMessage_ClosesTheConnection()
    {
        var connection = await ConnectAsync(Guid.NewGuid());

        await Assert.ThrowsAnyAsync<Exception>(() =>
            connection.InvokeCoreAsync("RequestStateSyncAsync", [new string('x', MaxMessageSizeBytes * 4)]));

        await EventuallyAsync(() => connection.State == HubConnectionState.Disconnected);
    }

    // ---- host helpers ----

    private HubConnection NewConnection(Guid userId, string path = "/hubs/auction")
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_baseUrl + path, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.AccessTokenProvider = () => Task.FromResult<string?>(userId.ToString());
            })
            .Build();
        _connections.Add(connection);
        return connection;
    }

    private async Task<HubConnection> ConnectAsync(Guid userId, string path = "/hubs/auction")
    {
        var connection = NewConnection(userId, path);
        await connection.StartAsync();
        return connection;
    }

    private static async Task TryStartAsync(HubConnection connection)
    {
        // The server may abort right after the handshake, so StartAsync can succeed or fail.
        try
        {
            await connection.StartAsync();
        }
        catch (Exception)
        {
            // Expected when the abort wins the race; the caller asserts on the final state.
        }
    }

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met within 10 seconds.");
            await Task.Delay(50);
        }
    }

    private static IMediator BuildMediator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<GetAuctionDetailsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<GetAuctionDetailsQuery>().AuctionId;
                return Task.FromResult(
                    id == MissingAuctionId
                        ? Result.Failure<AuctionDetailDto>(DomainErrors.Auction.NotFound)
                        : Result.Success(new AuctionDetailDto(
                            id, "Test", "Test", 100m, "USD", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), AuctionStatus.Active, 0)));
            });
        return mediator;
    }

    // ---- in-memory stand-ins for the Redis-backed services ----

    private sealed class InMemoryRateLimiter : IRateLimiter
    {
        private readonly ConcurrentDictionary<string, int> _counts = new();

        public Task<RateLimitResult> IsAllowedAsync(string key, CancellationToken cancellationToken = default) =>
            IsAllowedAsync(key, int.MaxValue, TimeSpan.FromMinutes(1), cancellationToken);

        public Task<RateLimitResult> IsAllowedAsync(
            string key, int limit, TimeSpan window, CancellationToken cancellationToken = default)
        {
            // Counts for the lifetime of the test host; the test window is a minute.
            var count = _counts.AddOrUpdate(key, 1, (_, current) => current + 1);
            return Task.FromResult(new RateLimitResult(count <= limit, Math.Min(count, limit)));
        }
    }

    private sealed class InMemoryConnectionTracker : IConnectionTracker
    {
        private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

        public int Count(Guid userId) => GetSnapshot(userId).Length;

        public Task TrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
        {
            var set = _connections.GetOrAdd(userId, _ => []);
            lock (set)
            {
                set.Add(connectionId);
            }

            return Task.CompletedTask;
        }

        public Task UntrackAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
        {
            if (_connections.TryGetValue(userId, out var set))
            {
                lock (set)
                {
                    set.Remove(connectionId);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>(GetSnapshot(userId));

        private string[] GetSnapshot(Guid userId)
        {
            if (!_connections.TryGetValue(userId, out var set))
            {
                return [];
            }

            lock (set)
            {
                return [.. set];
            }
        }
    }

    /// <summary>Authenticates by treating the bearer / <c>access_token</c> value as the user's GUID.</summary>
    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = Request.Query["access_token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
            }

            if (!Guid.TryParse(token, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
