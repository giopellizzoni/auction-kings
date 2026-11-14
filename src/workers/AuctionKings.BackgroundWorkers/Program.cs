using AuctionKings.Application;
using AuctionKings.BackgroundWorkers;
using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

DotNetEnv.Env.TraversePath().Load(".env.local");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddWorkerOptions(builder.Configuration);

// The worker hosts no SignalR: it hands real-time notifications to the API through a Redis Stream.
builder.Services.AddRealtimeStreamPublisher();

builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<AuctionCloseWorker>();
builder.Services.AddHostedService<AuctionActivationWorker>();
builder.Services.AddHostedService<CleanupExpiredDataWorker>();

var host = builder.Build();
await host.RunAsync();
