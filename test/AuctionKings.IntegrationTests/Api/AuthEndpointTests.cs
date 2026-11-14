using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuctionKings.IntegrationTests.Infrastructure;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>Registration and sign-in through the real API pipeline, database and token validation.</summary>
[Collection("AuthApi")]
public sealed class AuthEndpointTests(AuthApiFactory factory)
{
    private const string Password = "correct horse battery staple";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<HttpResponseMessage> Register(string email, string password = Password, string name = "Ann Bidder") =>
        await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new { Email = email, DisplayName = name, Password = password });

    private async Task<HttpResponseMessage> Login(string email, string password) =>
        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    [Fact]
    public async Task Register_creates_a_bidder_and_returns_a_token_that_the_api_accepts()
    {
        var email = UniqueEmail();

        var response = await Register(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.True(body.GetProperty("expiresIn").GetInt32() > 0);
        var user = body.GetProperty("user");
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Bidder", user.GetProperty("role").GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));

        // The token works on a protected endpoint for the user's own data.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
        var bids = await client.GetAsync($"/api/v1/users/{user.GetProperty("id").GetGuid()}/bids");
        Assert.Equal(HttpStatusCode.OK, bids.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_an_email_already_taken_regardless_of_case()
    {
        var email = UniqueEmail();
        Assert.Equal(HttpStatusCode.Created, (await Register(email)).StatusCode);

        var duplicate = await Register(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("User.EmailAlreadyRegistered", (await Json(duplicate)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("not-an-email", Password)]
    [InlineData("valid@example.com", "short")]
    public async Task Register_rejects_invalid_input_with_400(string email, string password)
    {
        var response = await Register(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_a_token_for_the_same_user()
    {
        var email = UniqueEmail();
        var registered = await Json(await Register(email));

        var response = await Login($"  {email.ToUpperInvariant()} ", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(
            registered.GetProperty("user").GetProperty("id").GetGuid(),
            body.GetProperty("user").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Login_answers_a_wrong_password_and_an_unknown_email_the_same_way()
    {
        var email = UniqueEmail();
        await Register(email);

        var wrongPassword = await Login(email, "not the password");
        var unknownEmail = await Login(UniqueEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await Json(wrongPassword)).GetProperty("message").GetString(),
            (await Json(unknownEmail)).GetProperty("message").GetString());
    }
}
