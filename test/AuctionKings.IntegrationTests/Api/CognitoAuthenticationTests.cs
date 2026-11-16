using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuctionKings.IntegrationTests.Infrastructure;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>
/// Cognito access tokens (issued by the Moto emulator) through the real API pipeline: signature via the pool's
/// JWKS, issuer, token type, app client, and <c>cognito:groups</c> as roles.
/// </summary>
[Collection("AuthApi")]
public sealed class CognitoAuthenticationTests(AuthApiFactory factory)
{
    private const string Password = "Correct-horse-42";

    private static readonly object NewAuction = new
    {
        Title = "Vintage camera",
        Description = "Works perfectly",
        StartingPrice = 100m,
        Currency = "USD",
        DurationInHours = 24,
    };

    private static string UniqueEmail() => $"cognito-{Guid.NewGuid():N}@example.com";

    private HttpClient ClientWith(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task A_Bidder_group_member_can_create_an_auction_and_read_their_own_bids()
    {
        var email = UniqueEmail();
        var sub = await factory.Cognito.CreateUserAsync(email, Password, "Bidder");
        var (accessToken, _) = await factory.Cognito.SignInAsync(email, Password);
        var client = ClientWith(accessToken);

        var created = await client.PostAsJsonAsync("/api/v1/auctions", NewAuction);
        var bids = await client.GetAsync($"/api/v1/users/{sub}/bids");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bids.StatusCode); // the Cognito sub is the user id
    }

    [Fact]
    public async Task A_user_in_no_group_is_authenticated_but_not_a_Bidder()
    {
        var email = UniqueEmail();
        var sub = await factory.Cognito.CreateUserAsync(email, Password);
        var (accessToken, _) = await factory.Cognito.SignInAsync(email, Password);
        var client = ClientWith(accessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auctions", NewAuction)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/users/{sub}/bids")).StatusCode);
    }

    [Fact]
    public async Task An_Admin_group_member_passes_the_Admin_policy()
    {
        var email = UniqueEmail();
        await factory.Cognito.CreateUserAsync(email, Password, "Admin");
        var (accessToken, _) = await factory.Cognito.SignInAsync(email, Password);

        // Unknown auction: authorization passes, the handler answers 404 (not 401/403).
        var response = await ClientWith(accessToken)
            .PostAsync($"/api/v1/auctions/{Guid.NewGuid()}/close", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_ID_token_is_rejected()
    {
        var email = UniqueEmail();
        var sub = await factory.Cognito.CreateUserAsync(email, Password, "Bidder");
        var (_, idToken) = await factory.Cognito.SignInAsync(email, Password);

        var response = await ClientWith(idToken).GetAsync($"/api/v1/users/{sub}/bids");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_from_an_app_client_the_api_does_not_accept_is_rejected()
    {
        var email = UniqueEmail();
        var sub = await factory.Cognito.CreateUserAsync(email, Password, "Bidder");
        var (accessToken, _) = await factory.Cognito.SignInAsync(email, Password, factory.Cognito.OtherClientId);

        var response = await ClientWith(accessToken).GetAsync($"/api/v1/users/{sub}/bids");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
