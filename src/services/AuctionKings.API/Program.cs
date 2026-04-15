using System.Text.Json;
using AuctionKings.API.Filters;
using AuctionKings.API.Hubs;
using AuctionKings.API.Middleware;
using AuctionKings.API.Realtime;
using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString);
builder.Services.AddSingleton<IRealtimeNotificationService, SignalRNotificationService>();

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
builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        if (httpContext.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid))
        {
            diagnosticContext.Set("CorrelationId", cid.ToString());
        }
    };
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(
        "/scalar/{documentName}",
        options =>
        {
            options.Title = "AuctionKings API";
        }
    );
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AuctionHub>("/hubs/auction");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapHealthChecks("/health/live");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
});

app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = WriteHealthResponse,
});

static Task WriteHealthResponse(HttpContext httpContext, HealthReport report)
{
    httpContext.Response.ContentType = "application/json";
    var result = JsonSerializer.Serialize(new
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
    });
    return httpContext.Response.WriteAsync(result);
}

app.Run();
