using AuctionKings.Application;
using AuctionKings.BackgroundWorkers;
using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

DotNetEnv.Env.TraversePath().Load(".env.local");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<OutboxOptions>(
    builder.Configuration.GetSection(OutboxOptions.SectionName)
);

builder.Services.Configure<AuctionCloseWorkerOptions>(
    builder.Configuration.GetSection(AuctionCloseWorkerOptions.SectionName));
builder.Services.Configure<AuctionActivationWorkerOptions>(
    builder.Configuration.GetSection(AuctionActivationWorkerOptions.SectionName));
builder.Services.Configure<CleanupWorkerOptions>(
    builder.Configuration.GetSection(CleanupWorkerOptions.SectionName));

builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<AuctionCloseWorker>();
builder.Services.AddHostedService<AuctionActivationWorker>();
builder.Services.AddHostedService<CleanupExpiredDataWorker>();

var host = builder.Build();
await host.RunAsync();
