using System.Text;
using System.Text.Json;
using AuctionKings.API.Auth;
using AuctionKings.API.Cors;
using AuctionKings.API.Filters;
using AuctionKings.API.Hubs;
using AuctionKings.API.Middleware;
using AuctionKings.API.Realtime;
using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.HealthChecks;
using AuctionKings.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;

DotNetEnv.Env.TraversePath().Load(".env.local");
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString);
builder.Services.AddSingleton<IRealtimeNotificationService, SignalRNotificationService>();

builder.Services.AddApiCors();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
    options.Filters.Add<IdempotencyFilter>();
});
builder.Services.AddOpenApi(
    "v1",
    options =>
    {
        options.AddDocumentTransformer(
            (document, _, _) =>
            {
                document.Info.Title = "AuctionKings API";
                document.Info.Version = "v1";
                document.Info.Description = "Real-time auction platform API";
                return Task.CompletedTask;
            }
        );
    }
);

// JWT Authentication
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder
    .Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateOnStart();

var jwtOptions =
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        $"'{JwtOptions.SectionName}' configuration section is missing."
    );

// Validate eagerly: the signing key is built below, and a bad secret should fail here with an
// actionable message rather than as IDX10703 on the first request.
var jwtValidation = new JwtOptionsValidator().Validate(null, jwtOptions);
if (jwtValidation.Failed)
{
    throw new OptionsValidationException(
        Options.DefaultName,
        typeof(JwtOptions),
        jwtValidation.Failures!
    );
}

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
        };
        // SignalR WebSocket connections cannot send Authorization headers, so the JWT
        // is passed as the access_token query parameter instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs"))
                {
                    var token = context.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        context.Token = token;
                    }
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.Bidder, policy => policy.RequireRole(Policies.Bidder));
    options.AddPolicy(Policies.Admin, policy => policy.RequireRole(Policies.Admin));
});
builder.Services.AddSingleton<JwtTokenGenerator>();

builder.Services.AddHealthChecks().AddInfrastructureHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpMetrics();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
    };
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(
        "/scalar",
        options =>
        {
            options.Title = "AuctionKings API";
        }
    );
}

// CORS before HTTPS redirection (browsers reject redirected preflights) and before
// authentication (preflight requests carry no bearer token).
app.UseApiCors();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMetrics(); // exposes GET /metrics
app.MapHub<AuctionHub>("/hubs/auction");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapHealthChecks("/health/live");

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponse,
    }
);

app.MapHealthChecks(
    "/health/db",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db"),
        ResponseWriter = WriteHealthResponse,
    }
);

static Task WriteHealthResponse(HttpContext httpContext, HealthReport report)
{
    httpContext.Response.ContentType = "application/json";
    var result = JsonSerializer.Serialize(
        new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        }
    );
    return httpContext.Response.WriteAsync(result);
}

app.Run();
