using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace AuctionKings.IntegrationTests.Infrastructure;

/// <summary>
/// A Moto container emulating Amazon Cognito (the same emulator as docker-compose.infra.yml), driven through the
/// Cognito JSON API. Moto signs real RS256 access tokens with the real issuer URL and serves the pool's OpenID
/// configuration and JWKS, so the API validates its tokens exactly as it would Cognito's.
/// </summary>
public sealed class MotoCognito : IAsyncDisposable
{
    internal const string Region = "us-east-1";
    private const int MotoPort = 5555;

    private readonly IContainer _container = new ContainerBuilder("motoserver/moto:5.2.5")
        .WithCommand("-H", "0.0.0.0", "-p", MotoPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .WithPortBinding(MotoPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(MotoPort).ForPath("/moto-api/")))
        .Build();

    private static readonly string[] EmailUsername = ["email"];
    private static readonly string[] PasswordAuthFlows = ["ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"];

    private HttpClient? _http;

    private HttpClient Http => _http ?? throw new InvalidOperationException("Call StartAsync first.");

    public string UserPoolId { get; private set; } = string.Empty;

    /// <summary>The app client the API accepts.</summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>A second client of the same pool that the API does not accept.</summary>
    public string OtherClientId { get; private set; } = string.Empty;

    public string MetadataAddress =>
        $"{Http.BaseAddress}{UserPoolId}/.well-known/openid-configuration";

    public async Task StartAsync()
    {
        await _container.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(MotoPort)}/") };

        var pool = await Call("CreateUserPool", new { PoolName = "auction-kings-tests", UsernameAttributes = EmailUsername });
        UserPoolId = pool.GetProperty("UserPool").GetProperty("Id").GetString()!;
        ClientId = await CreateClient("api");
        OtherClientId = await CreateClient("other");

        foreach (var group in new[] { "Bidder", "Admin" })
        {
            await Call("CreateGroup", new { UserPoolId, GroupName = group });
        }
    }

    /// <summary>Creates a confirmed user (optionally in groups) and returns its Cognito <c>sub</c>.</summary>
    public async Task<Guid> CreateUserAsync(string email, string password, params string[] groups)
    {
        var signUp = await Call("SignUp", new
        {
            ClientId,
            Username = email,
            Password = password,
            UserAttributes = new[] { new { Name = "email", Value = email } },
        });
        await Call("AdminConfirmSignUp", new { UserPoolId, Username = email });

        foreach (var group in groups)
        {
            await Call("AdminAddUserToGroup", new { UserPoolId, Username = email, GroupName = group });
        }

        return Guid.Parse(signUp.GetProperty("UserSub").GetString()!);
    }

    /// <summary>Signs in with USER_PASSWORD_AUTH; returns (access token, ID token).</summary>
    public async Task<(string AccessToken, string IdToken)> SignInAsync(string email, string password, string? clientId = null)
    {
        var auth = await Call("InitiateAuth", new
        {
            ClientId = clientId ?? ClientId,
            AuthFlow = "USER_PASSWORD_AUTH",
            AuthParameters = new Dictionary<string, string> { ["USERNAME"] = email, ["PASSWORD"] = password },
        });
        var result = auth.GetProperty("AuthenticationResult");
        return (result.GetProperty("AccessToken").GetString()!, result.GetProperty("IdToken").GetString()!);
    }

    private async Task<string> CreateClient(string name)
    {
        var client = await Call("CreateUserPoolClient", new
        {
            UserPoolId,
            ClientName = name,
            ExplicitAuthFlows = PasswordAuthFlows,
        });
        return client.GetProperty("UserPoolClient").GetProperty(nameof(ClientId)).GetString()!;
    }

    private async Task<JsonElement> Call(string action, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/x-amz-json-1.1"),
        };
        request.Headers.Add("X-Amz-Target", $"AWSCognitoIdentityProviderService.{action}");

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Moto {action} failed ({(int)response.StatusCode}): {text}");
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        await _container.DisposeAsync();
    }
}
