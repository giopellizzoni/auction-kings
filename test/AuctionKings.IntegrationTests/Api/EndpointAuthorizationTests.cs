using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuctionKings.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionKings.IntegrationTests.Api;

/// <summary>
/// Locks in authorization behaviour of every HTTP endpoint: an audit over the real endpoint
/// data source (so a new unprotected endpoint fails the suite) plus explicit 401/403/2xx checks
/// for the key flows (#138).
/// </summary>
[Collection("AuthApi")]
public sealed class EndpointAuthorizationTests(AuthApiFactory factory)
{
    /// <summary>
    /// Endpoints that are intentionally reachable without authentication, keyed by route pattern.
    /// Adding an entry requires a reason; an endpoint that is neither here nor protected fails the audit.
    /// </summary>
    private static readonly Dictionary<string, string> AnonymousAllowlist = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["api/v1/auctions"] = "Public listing of active auctions (GET). Bidding data is not exposed here.",
        ["api/v1/auctions/{id:guid}"] = "Public auction details (GET).",
        ["dev/token"] = "Dev-only token minting; returns 404 unless Features:EnableDevTokenEndpoint is set (see test below).",
        ["health/live"] = "Liveness probe for the orchestrator/ALB.",
        ["health/ready"] = "Readiness probe for the orchestrator/ALB.",
        ["health/db"] = "Database probe for the orchestrator/ALB.",
        ["metrics"] = "Prometheus scrape endpoint (network-restricted, not internet-facing).",
        ["openapi/{documentName}.json"] = "OpenAPI document, only mapped in Development.",
        ["scalar/{documentName?}"] = "API reference UI, only mapped in Development.",
        ["scalar/scalar.js"] = "API reference UI static asset, only mapped in Development.",
        ["scalar/scalar.aspnetcore.js"] = "API reference UI static asset, only mapped in Development.",
        ["scalar/favicon.svg"] = "API reference UI static asset, only mapped in Development.",
    };

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private HttpClient AnonymousClient() => factory.CreateClient();

    private HttpClient ClientFor(Guid userId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateToken(userId, role)
        );
        return client;
    }

    private static string? Raw(RouteEndpoint e) => e.RoutePattern.RawText?.TrimStart('/');

    private static string Describe(RouteEndpoint e)
    {
        var methods = e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return $"{(methods is null ? "*" : string.Join(',', methods))} /{Raw(e)}";
    }

    private IEnumerable<RouteEndpoint> AllEndpoints() =>
        factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>();

    private static bool RequiresAuthorization(RouteEndpoint e) =>
        e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
        && e.Metadata.GetMetadata<IAllowAnonymous>() is null;

    private static bool IsAllowlisted(RouteEndpoint e) =>
        Raw(e) is { } raw && AnonymousAllowlist.ContainsKey(raw);

    [Fact]
    public void Endpoint_audit_finds_endpoints()
    {
        // Guards the audit itself: an empty enumeration would make the tests below pass vacuously.
        var endpoints = AllEndpoints().ToList();
        Assert.Contains(endpoints, e => Raw(e) == "api/v1/auctions/{auctionId:guid}/bids");
        Assert.Contains(endpoints, e => Raw(e) == "hubs/auction");
        Assert.Contains(endpoints, e => Raw(e) == "hubs/notifications");
    }

    [Fact]
    public void Every_endpoint_requires_authorization_unless_explicitly_allowlisted()
    {
        var unprotected = AllEndpoints()
            .Where(e => !RequiresAuthorization(e) && !IsAllowlisted(e))
            .Select(Describe)
            .Distinct()
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            "Endpoints with no [Authorize] that are not in the anonymous allowlist: "
                + string.Join("; ", unprotected)
        );
    }

    [Fact]
    public void Anonymous_allowlist_has_no_stale_entries()
    {
        var present = AllEndpoints()
            .Select(Raw)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = AnonymousAllowlist.Keys.Where(k => !present.Contains(k)).ToList();

        Assert.True(stale.Count == 0, "Allowlist entries matching no endpoint: " + string.Join(", ", stale));
    }

    [Fact]
    public void Allowlisted_endpoints_that_mutate_state_are_not_anonymous_writes()
    {
        // The only anonymous POST allowed is the (flag-gated) dev token endpoint.
        var anonymousWrites = AllEndpoints()
            .Where(e => !RequiresAuthorization(e) && IsAllowlisted(e))
            .Where(e =>
                e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Any(m =>
                    !HttpMethods.IsGet(m) && !HttpMethods.IsHead(m)
                ) == true
            )
            .Select(Describe)
            .ToList();

        Assert.Equal(["POST /dev/token"], anonymousWrites);
    }

    [Fact]
    public async Task Dev_token_endpoint_is_not_found_when_feature_flag_is_off()
    {
        var response = await AnonymousClient()
            .PostAsJsonAsync("/dev/token", new { UserId, Role = "Admin" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auctions")]
    [InlineData("POST", "/api/v1/auctions/33333333-3333-3333-3333-333333333333/close")]
    [InlineData("POST", "/api/v1/auctions/33333333-3333-3333-3333-333333333333/bids")]
    [InlineData("GET", "/api/v1/auctions/33333333-3333-3333-3333-333333333333/bids")]
    [InlineData("GET", "/api/v1/users/11111111-1111-1111-1111-111111111111/bids")]
    public async Task Protected_endpoint_returns_401_without_a_token(string method, string url)
    {
        var response = await AnonymousClient().SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auctions")]
    [InlineData("GET", "/api/v1/auctions/33333333-3333-3333-3333-333333333333/bids")]
    [InlineData("GET", "/api/v1/users/11111111-1111-1111-1111-111111111111/bids")]
    public async Task Protected_endpoint_returns_401_for_a_token_signed_with_the_wrong_key(
        string method,
        string url
    )
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthApiFactory.CreateTokenWithWrongKey(UserId, "Bidder")
        );

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_for_a_malformed_token()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("/api/v1/users/" + UserId + "/bids");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_returns_403_for_a_token_without_the_Bidder_role()
    {
        var response = await ClientFor(UserId, "Viewer")
            .PostAsJsonAsync("/api/v1/auctions", ValidAuction());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_returns_403_for_an_Admin_only_token()
    {
        // Policies are role-exact, not hierarchical: Admin does not imply Bidder.
        var response = await ClientFor(UserId, "Admin")
            .PostAsJsonAsync("/api/v1/auctions", ValidAuction());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAuction_returns_201_for_a_Bidder()
    {
        var response = await ClientFor(UserId, "Bidder")
            .PostAsJsonAsync("/api/v1/auctions", ValidAuction());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PlaceBid_returns_403_for_a_token_without_the_Bidder_role()
    {
        var request = Request("POST", $"/api/v1/auctions/{Guid.NewGuid()}/bids");
        request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { Amount = 10m, Currency = "USD" });

        var response = await ClientFor(UserId, "Viewer").SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlaceBid_is_authorized_for_a_Bidder_and_reaches_the_handler()
    {
        // Unknown auction: authorization passes, the handler answers 404 (not 401/403).
        var request = Request("POST", $"/api/v1/auctions/{Guid.NewGuid()}/bids");
        request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { Amount = 10m, Currency = "USD" });

        var response = await ClientFor(UserId, "Bidder").SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CloseAuction_returns_403_for_a_Bidder()
    {
        var response = await ClientFor(UserId, "Bidder")
            .PostAsync($"/api/v1/auctions/{Guid.NewGuid()}/close", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CloseAuction_is_authorized_for_an_Admin_and_reaches_the_handler()
    {
        var response = await ClientFor(UserId, "Admin")
            .PostAsync($"/api/v1/auctions/{Guid.NewGuid()}/close", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBids_returns_200_for_any_authenticated_user()
    {
        // [Authorize] without a role: even a user with no role may read the (anonymised) bid list.
        var created = await ClientFor(UserId, "Bidder")
            .PostAsJsonAsync("/api/v1/auctions", ValidAuction());
        var auctionId = await created.Content.ReadFromJsonAsync<Guid>();

        var response = await ClientFor(OtherUserId, "Viewer")
            .GetAsync($"/api/v1/auctions/{auctionId}/bids");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserBidHistory_returns_403_when_reading_another_users_history()
    {
        var response = await ClientFor(OtherUserId, "Bidder").GetAsync($"/api/v1/users/{UserId}/bids");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserBidHistory_returns_200_for_the_owner()
    {
        var response = await ClientFor(UserId, "Bidder").GetAsync($"/api/v1/users/{UserId}/bids");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserBidHistory_returns_200_for_an_Admin_reading_another_users_history()
    {
        var response = await ClientFor(OtherUserId, "Admin").GetAsync($"/api/v1/users/{UserId}/bids");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auctions")]
    [InlineData("/health/live")]
    public async Task Intentionally_anonymous_endpoints_are_reachable_without_a_token(string url)
    {
        var response = await AnonymousClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage Request(string method, string url) => new(new HttpMethod(method), url);

    private static object ValidAuction() =>
        new
        {
            Title = "Authorization test auction",
            Description = "Created by the authorization tests",
            StartingPrice = 10m,
            Currency = "USD",
            DurationInHours = 2.0,
        };
}
