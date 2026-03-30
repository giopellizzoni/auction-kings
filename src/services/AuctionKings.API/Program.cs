using AuctionKings.API.Hubs;
using AuctionKings.API.Realtime;
using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString);
builder.Services.AddSingleton<IRealtimeNotificationService, SignalRNotificationService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AuctionHub>("/hubs/auction");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapHealthChecks("/health/live");

app.Run();
