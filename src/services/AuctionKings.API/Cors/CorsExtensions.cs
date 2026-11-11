using AuctionKings.API.Middleware;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace AuctionKings.API.Cors;

public static class CorsExtensions
{
    internal static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE"];

    internal static readonly string[] AllowedHeaders =
    [
        "Authorization",
        "Content-Type",
        "X-Request-Id",
        CorrelationIdMiddleware.HeaderName,
        "X-Requested-With",
        "X-SignalR-User-Agent",
    ];

    internal static readonly string[] ExposedHeaders =
    [
        CorrelationIdMiddleware.HeaderName,
        "X-RateLimit-Limit",
        "X-RateLimit-Remaining",
        "Retry-After",
    ];

    internal static readonly TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Registers the default CORS policy and the WebSocket origin allowlist, both driven by the
    /// validated <see cref="CorsSettings"/>. Invalid origins fail the host at startup.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        services.AddOptions<CorsSettings>().BindConfiguration(CorsSettings.SectionName).ValidateOnStart();

        services.AddCors();

        // Configure from IOptions<CorsSettings> (not raw configuration) so validation always
        // runs before the policy is built.
        services
            .AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>(
                (cors, settings) =>
                    cors.AddDefaultPolicy(policy =>
                        policy
                            .WithOrigins(settings.Value.AllowedOrigins)
                            .WithMethods(AllowedMethods)
                            .WithHeaders(AllowedHeaders)
                            .WithExposedHeaders(ExposedHeaders)
                            .AllowCredentials()
                            .SetPreflightMaxAge(PreflightMaxAge)
                    )
            );

        // CORS does not apply to WebSocket upgrades, so the same allowlist is enforced on the
        // WebSocket middleware (403 for a browser Origin not in the list). SignalR's own inner
        // UseWebSockets() resolves these same options. Note: an empty list means the WebSocket
        // middleware performs no origin check — hub access still requires a valid JWT.
        services
            .AddOptions<WebSocketOptions>()
            .Configure<IOptions<CorsSettings>>(
                (webSockets, settings) =>
                {
                    foreach (var origin in settings.Value.AllowedOrigins)
                    {
                        webSockets.AllowedOrigins.Add(origin);
                    }
                }
            );

        return services;
    }

    /// <summary>
    /// Adds CORS and WebSocket origin enforcement. Must run before HTTPS redirection
    /// (browsers fail redirected preflights) and before authentication (preflights carry no token).
    /// </summary>
    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app)
    {
        app.UseCors();
        app.UseWebSockets();
        return app;
    }
}
