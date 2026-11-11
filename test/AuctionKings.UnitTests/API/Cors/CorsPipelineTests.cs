using AuctionKings.API.Cors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AuctionKings.UnitTests.API.Cors;

/// <summary>
/// Exercises the real CORS + WebSocket middleware registered by <see cref="CorsExtensions"/>
/// through an <see cref="ApplicationBuilder"/> pipeline, without starting the full API host.
/// </summary>
public sealed class CorsPipelineTests
{
    private const string AllowedOrigin = "https://app.example.com";
    private const string OtherOrigin = "https://evil.example.com";

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ShouldReturnCorsHeadersAndShortCircuit()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreatePreflight(services, AllowedOrigin, "POST", "authorization,content-type,x-request-id");

        await pipeline(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(AllowedOrigin, context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Equal("true", context.Response.Headers.AccessControlAllowCredentials.ToString());
        Assert.Equal("600", context.Response.Headers.AccessControlMaxAge.ToString());
        var allowedHeaders = context.Response.Headers.AccessControlAllowHeaders.ToString();
        Assert.Contains("X-Request-Id", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Authorization", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.False(context.Items.ContainsKey(TerminalMarker));
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_ShouldNotAllowOrigin()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreatePreflight(services, OtherOrigin, "POST", "content-type");

        await pipeline(context);

        Assert.Equal(string.Empty, context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task Preflight_WithDisallowedMethod_ShouldNotAllowMethod()
    {
        // ASP.NET Core still echoes the origin but omits the method; the browser then blocks it.
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreatePreflight(services, AllowedOrigin, "PATCH", null);

        await pipeline(context);

        Assert.DoesNotContain(
            "PATCH",
            context.Response.Headers.AccessControlAllowMethods.ToString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Preflight_WithUnlistedHeader_ShouldNotAllowHeader()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreatePreflight(services, AllowedOrigin, "POST", "x-custom-secret");

        await pipeline(context);

        Assert.DoesNotContain(
            "x-custom-secret",
            context.Response.Headers.AccessControlAllowHeaders.ToString(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Preflight_WithEmptyAllowlist_ShouldNotAllowAnyOrigin()
    {
        var (pipeline, services) = BuildPipeline();
        var context = CreatePreflight(services, AllowedOrigin, "GET", null);

        await pipeline(context);

        Assert.Equal(string.Empty, context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task ActualRequest_FromAllowedOrigin_ShouldAllowOriginAndExposeHeaders()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var (context, response) = CreateRequest(services, HttpMethods.Get, AllowedOrigin);

        await pipeline(context);
        await response.StartAsync();

        Assert.True(context.Items.ContainsKey(TerminalMarker));
        Assert.Equal(AllowedOrigin, context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Equal("true", context.Response.Headers.AccessControlAllowCredentials.ToString());
        var exposed = context.Response.Headers.AccessControlExposeHeaders.ToString();
        Assert.Contains("X-Correlation-Id", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-RateLimit-Remaining", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry-After", exposed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActualRequest_FromDisallowedOrigin_ShouldNotAllowOrigin()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var (context, response) = CreateRequest(services, HttpMethods.Get, OtherOrigin);

        await pipeline(context);
        await response.StartAsync();

        Assert.Equal(string.Empty, context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task ActualRequest_WithoutOrigin_ShouldPassThroughWithoutCorsHeaders()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var (context, response) = CreateRequest(services, HttpMethods.Get, origin: null);

        await pipeline(context);
        await response.StartAsync();

        Assert.True(context.Items.ContainsKey(TerminalMarker));
        Assert.Equal(string.Empty, context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task WebSocketUpgrade_FromDisallowedOrigin_ShouldReturn403()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreateWebSocketUpgrade(services, OtherOrigin);

        await pipeline(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(context.Items.ContainsKey(TerminalMarker));
    }

    [Fact]
    public async Task WebSocketUpgrade_FromAllowedOrigin_ShouldReachHub()
    {
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreateWebSocketUpgrade(services, AllowedOrigin);

        await pipeline(context);

        Assert.True(context.Items.ContainsKey(TerminalMarker));
    }

    [Fact]
    public async Task WebSocketUpgrade_WithoutOrigin_ShouldReachHub()
    {
        // Non-browser clients (the .NET SignalR client, load tests) send no Origin header.
        var (pipeline, services) = BuildPipeline(AllowedOrigin);
        var context = CreateWebSocketUpgrade(services, origin: null);

        await pipeline(context);

        Assert.True(context.Items.ContainsKey(TerminalMarker));
    }

    [Fact]
    public void Startup_WithWildcardOrigin_ShouldFailValidation()
    {
        using var provider = BuildServices("*");

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CorsSettings>>().Value);
    }

    private const string TerminalMarker = "terminal-reached";

    private static ServiceProvider BuildServices(params string[] origins)
    {
        var values = origins
            .Select((origin, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))
            .ToList();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApiCors();
        return services.BuildServiceProvider();
    }

    private static (RequestDelegate Pipeline, ServiceProvider Services) BuildPipeline(params string[] origins)
    {
        var services = BuildServices(origins);
        var app = new ApplicationBuilder(services);
        app.UseApiCors();
        app.Run(context =>
        {
            context.Items[TerminalMarker] = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        return (app.Build(), services);
    }

    private static DefaultHttpContext CreatePreflight(
        IServiceProvider services,
        string origin,
        string requestMethod,
        string? requestHeaders
    )
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Options;
        context.Request.Path = "/api/auctions";
        context.Request.Headers.Origin = origin;
        context.Request.Headers.AccessControlRequestMethod = requestMethod;
        if (requestHeaders is not null)
        {
            context.Request.Headers.AccessControlRequestHeaders = requestHeaders;
        }
        return context;
    }

    private static (DefaultHttpContext Context, StartableResponseFeature Response) CreateRequest(
        IServiceProvider services,
        string method,
        string? origin
    )
    {
        var context = new DefaultHttpContext { RequestServices = services };
        // CorsMiddleware applies headers for actual requests in Response.OnStarting, which the
        // default response feature never fires — this feature lets the test start the response.
        var response = new StartableResponseFeature();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = method;
        context.Request.Path = "/api/auctions";
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }
        return (context, response);
    }

    private static DefaultHttpContext CreateWebSocketUpgrade(IServiceProvider services, string? origin)
    {
        var upgradeFeature = Substitute.For<IHttpUpgradeFeature>();
        upgradeFeature.IsUpgradableRequest.Returns(true);

        var context = new DefaultHttpContext { RequestServices = services };
        context.Features.Set(upgradeFeature);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/hubs/auction";
        context.Request.Headers.Connection = "Upgrade";
        context.Request.Headers.Upgrade = "websocket";
        context.Request.Headers.SecWebSocketVersion = "13";
        context.Request.Headers.SecWebSocketKey = Convert.ToBase64String(new byte[16]);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }
        return context;
    }

    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public async Task StartAsync()
        {
            // ASP.NET Core runs OnStarting callbacks in reverse registration order.
            for (var i = _onStarting.Count - 1; i >= 0; i--)
            {
                await _onStarting[i].Callback(_onStarting[i].State);
            }
        }
    }
}
