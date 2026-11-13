using System.Net;
using System.Net.Http.Json;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>
/// Anonymous clients cannot connect to either hub; authenticated clients can, over both transports
/// the real client uses (#139). WebSockets pass the JWT as the <c>access_token</c> query parameter,
/// which Program.cs reads for <c>/hubs</c> paths.
/// </summary>
[Collection("AuthApi")]
public sealed class HubAuthorizationTests(AuthApiFactory factory)
{
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static TheoryData<string> Hubs => new() { "/hubs/auction", "/hubs/notifications" };

    private HubConnection Build(
        string hubPath,
        HttpTransportType transport,
        Func<Task<string?>>? accessTokenProvider
    ) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, hubPath),
                options =>
                {
                    options.Transports = transport;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    // The browser (JavaScript) client cannot set headers on a WebSocket, so it passes the
                    // JWT as ?access_token=. Mirror that here: this is the path Program.cs special-cases.
                    options.WebSocketFactory = async (context, cancellationToken) =>
                    {
                        var uri = context.Uri;
                        var token = accessTokenProvider is null ? null : await accessTokenProvider();
                        if (token is not null)
                        {
                            var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
                            uri = new Uri($"{uri}{separator}access_token={Uri.EscapeDataString(token)}");
                        }

                        return await factory.Server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                    };
                    options.AccessTokenProvider = accessTokenProvider;
                }
            )
            .Build();

    private Func<Task<string?>> TokenFor(string role) =>
        () => Task.FromResult<string?>(factory.CreateToken(UserId, role));

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Negotiate_without_a_token_returns_401(string hubPath)
    {
        var response = await factory.CreateClient().PostAsync($"{hubPath}/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Negotiate_with_a_token_succeeds(string hubPath)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1&access_token={factory.CreateToken(UserId, "Bidder")}",
            content: null
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Anonymous_connection_is_rejected_with_401_over_long_polling(string hubPath)
    {
        await using var connection = Build(hubPath, HttpTransportType.LongPolling, accessTokenProvider: null);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Anonymous_connection_is_rejected_over_websockets(string hubPath)
    {
        await using var connection = Build(hubPath, HttpTransportType.WebSockets, accessTokenProvider: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());

        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Connection_with_an_invalid_token_is_rejected(string hubPath)
    {
        await using var connection = Build(
            hubPath,
            HttpTransportType.LongPolling,
            () => Task.FromResult<string?>(AuthApiFactory.CreateTokenWithWrongKey(UserId, "Bidder"))
        );

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Authenticated_connection_is_accepted_over_websockets_using_the_access_token_query(
        string hubPath
    )
    {
        await using var connection = Build(hubPath, HttpTransportType.WebSockets, TokenFor("Bidder"));

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Authenticated_connection_is_accepted_over_long_polling(string hubPath)
    {
        await using var connection = Build(hubPath, HttpTransportType.LongPolling, TokenFor("Bidder"));

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Theory]
    [MemberData(nameof(Hubs))]
    public async Task Hubs_require_authentication_but_not_a_role(string hubPath)
    {
        // Hubs use plain [Authorize]: any valid token connects, whatever its role.
        await using var connection = Build(hubPath, HttpTransportType.WebSockets, TokenFor("Viewer"));

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Authenticated_client_can_invoke_AuctionHub_methods()
    {
        var auctionId = await CreateAuctionAsync();
        await using var connection = Build("/hubs/auction", HttpTransportType.WebSockets, TokenFor("Bidder"));
        var state = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<object>("AuctionState", s => state.TrySetResult(s));
        await connection.StartAsync();

        await connection.InvokeAsync("JoinAuctionAsync", auctionId);

        var received = await state.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(received);
    }

    [Fact]
    public async Task Joining_an_unknown_auction_is_rejected_and_the_connection_survives()
    {
        await using var connection = Build("/hubs/auction", HttpTransportType.WebSockets, TokenFor("Bidder"));
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("JoinAuctionAsync", Guid.NewGuid())
        );

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task Hub_method_cannot_be_invoked_by_a_client_that_never_authenticated()
    {
        // No connection means no invocation: a client that never authenticated cannot reach any hub method.
        await using var connection = Build("/hubs/auction", HttpTransportType.LongPolling, accessTokenProvider: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.InvokeAsync("JoinAuctionAsync", Guid.NewGuid())
        );
    }

    private async Task<Guid> CreateAuctionAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(UserId, "Bidder"));
        var response = await client.PostAsJsonAsync(
            "/api/v1/auctions",
            new
            {
                Title = "Hub test auction",
                Description = "Created by the hub authorization tests",
                StartingPrice = 10m,
                Currency = "USD",
                DurationInHours = 2.0,
            }
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
